using Octopus.Player.Core.Maths;
using Octopus.Player.GPU;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using System;
using System.Diagnostics;
using System.IO;

namespace Octopus.Player.Core.Playback
{
    public class SequenceFrameDNG : SequenceFrameRAW
    {
        private IO.DNG.Reader DNGReader { get; set; }

        public SequenceFrameDNG(GPU.Compute.IContext computeContext, GPU.Compute.IQueue computeQueue, IClip clip, GPU.Format format)
            : base(computeContext, computeQueue, clip, format)
        {

        }

        Error TryDecode(IClip clip, byte[] workingBuffer = null)
        {
            // Cast to DNG clip/metadata
            var dngClip = (ClipCinemaDNG)clip;
            var dngMetadata = (IO.DNG.MetadataCinemaDNG)dngClip.Metadata;
            Debug.Assert(dngClip != null && dngMetadata != null);

            // Sanity check the frame number is valid
            if (frameNumber > dngMetadata.LastFrame || frameNumber < dngMetadata.FirstFrame)
                return Error.BadFrameIndex;

            // Get and check the dng frame path
            string framePath;
            var getFrameResult = dngClip.GetFramePath(frameNumber, out framePath);
            if (getFrameResult != Error.None)
                return getFrameResult;
            if (!File.Exists(framePath))
                return Error.FrameNotPresent;

            // Create a new DNG reader for this frame
            if (DNGReader != null)
                DNGReader.Dispose();
            DNGReader = null;
            DNGReader = new IO.DNG.Reader(framePath);
            if (!DNGReader.Valid)
            {
                DNGReader.Dispose();
                DNGReader = null;
                return Error.BadFrame;
            }

            // Read timecode
            if ( DNGReader.ContainsTimeCode )
                timeCode = new TimeCode(DNGReader.TimeCode);

            // Read/decode the data
            var decodeDataError = Error.None;
            switch (DNGReader.Compression)
            {
                case IO.DNG.Compression.None:
                case IO.DNG.Compression.LosslessJPEG:
                    var bytesPerPixel = clip.Metadata.BitDepth <= 8 ? 1 : 2;

                    // Decode and copy to GPU
                    Debug.Assert(decodedImageGpu != null && decodedImageGpu.Dimensions == clip.Metadata.PaddedDimensions);
                    var decodedImage = System.Buffers.ArrayPool<byte>.Shared.Rent(bytesPerPixel * clip.Metadata.PaddedDimensions.Area());
                    decodeDataError = DNGReader.DecodeImageData(decodedImage, workingBuffer);
                    try
                    {
                        if (decodeDataError == Error.None)
                        {
                            var metadata = (IO.DNG.MetadataCinemaDNG)clip.Metadata;
                            if (metadata.TileCount > 0)
                                ForEachTile(metadata, (origin, size, offset) => { ComputeQueue.ModifyImage(decodedImageGpu, origin, size, decodedImage, offset); });
                            else
                                ComputeQueue.ModifyImage(decodedImageGpu, Vector2i.Zero, decodedImageGpu.Dimensions, decodedImage);
                        }
                    }
                    catch
                    {
                        decodeDataError = Error.ComputeError;
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(decodedImage);
                    }
                    break;

                default:
                    DNGReader.Dispose();
                    DNGReader = null;
                    return Error.NotImplmeneted;
            }

            // Done
            DNGReader.Dispose();
            DNGReader = null;
            return decodeDataError;
        }

        public override Error Decode(IClip clip, byte[] workingBuffer = null)
        {
            var result = TryDecode(clip, workingBuffer);

            // Blank data if we didn't decode properly
            if (result != Error.None)
                ComputeQueue.Memset(decodedImageGpu, Vector4.Zero);
            
            LastError = result;
            Processed = false;
            return result;
        }

        private void ForEachTile(IO.DNG.MetadataCinemaDNG metadata, Action<Vector2i, Vector2i, uint> action)
        {
            uint dataOffset = 0;
            var tileSizeBytes = (uint)(metadata.TileDimensions.Area() * metadata.DecodedBitDepth) / 8;
            for (int y = 0; y < metadata.PaddedDimensions.Y; y += metadata.TileDimensions.Y)
            {
                for (int x = 0; x < metadata.PaddedDimensions.X; x += metadata.TileDimensions.X)
                {
                    var tileDimensions = metadata.TileDimensions;
                    var maxTileSize = metadata.PaddedDimensions - new Vector2i(x, y);
                    tileDimensions.X = Math.Min(maxTileSize.X, tileDimensions.X);
                    tileDimensions.Y = Math.Min(maxTileSize.Y, tileDimensions.Y);
                    action(new Vector2i(x, y), tileDimensions, dataOffset);
                    dataOffset += tileSizeBytes;
                }
            }
        }

        public override Error Process(IClip clip, IContext renderContext, GPU.Compute.IImage2D output, GPU.Compute.IImage1D linearizeTable, GPU.Compute.IProgram program,
            GPU.Compute.IQueue queue, IO.LUT.ILUT3D logToDisplay, bool immediate = false, Action postProcessAction = null)
        {
            Debug.Assert(clip.GetType() == typeof(ClipCinemaDNG));
            var metadata = (IO.DNG.MetadataCinemaDNG)clip.Metadata;
            Debug.Assert(metadata != null);

            Action renderAction = () =>
            {
                bool useLut = logToDisplay != null && logToDisplay.ComputeImage != null;
                uint argumentIndex = 0;
                var kernel = ComputeKernelForClip(metadata, useLut);
                program.SetArgument(kernel, argumentIndex++, decodedImageGpu);

                // Calculate and apply black/white levels
                var blackWhiteLevel = new Vector2(metadata.BlackLevel, metadata.WhiteLevel);
                var decodedMaxlevel = (1 << (int)metadata.DecodedBitDepth) - 1;
                var linearMaxLevel = decodedMaxlevel;
                if (metadata.LinearizationTable != null && metadata.LinearizationTable.Length > 0 && linearizeTable != null)
                    linearMaxLevel = (1 << (linearizeTable.Format.SizeBytes() * 8)) - 1;
                program.SetArgument(kernel, argumentIndex++, blackWhiteLevel / linearMaxLevel);

                // Calculate and apply exposure
                var exposure = Math.Pow(2.0, clip.RawParameters.Value.exposure.HasValue ? clip.RawParameters.Value.exposure.Value : metadata.ExposureValue);
                program.SetArgument(kernel, argumentIndex++, (float)exposure);

                // Tone mapping operator
                program.SetArgument(kernel, argumentIndex++, (int)clip.RawParameters.Value.toneMappingOperator.GetValueOrDefault(ToneMappingOperator.SDR));

                // Gamma
                var gammaSpace = clip.RawParameters.Value.gammaSpace.GetValueOrDefault(GammaSpace.Rec709);
                program.SetArgument(kernel, argumentIndex++, (int)gammaSpace);

                // Apply log to display LUT
                if (useLut)
                    program.SetArgument(kernel, argumentIndex++, logToDisplay.ComputeImage);

                // Set output
                program.SetArgument(kernel, argumentIndex++, output);

                // Colour only options
                if (metadata.ColorProfile.HasValue)
                {
                    program.SetArgument(kernel, argumentIndex++, (int)clip.RawParameters.Value.highlightRecovery.GetValueOrDefault(HighlightRecovery.On));

                    // Combine camera to xyz/xyz to display colour matrices
                    var cameraToXYZD50Matrix = metadata.ColorProfile.Value.CalculateCameraToXYZD50(clip.RawParameters.Value.whiteBalance);
                    var xyzToDisplayColourMatrix = gammaSpace.ColourSpaceTransformD50();
                    var cameraToDisplayColourMatrix = Maths.Color.Matrix.NormalizeColourMatrix(xyzToDisplayColourMatrix) * cameraToXYZD50Matrix;

                    // Calculate camera white in RAW space
                    var cameraToDisplayInv = Matrix3.Invert(cameraToDisplayColourMatrix);
                    var whiteLevelCamera = cameraToDisplayInv * Vector3.One;
                    var cameraWhiteMin = Math.Min(Math.Min(whiteLevelCamera.X, whiteLevelCamera.Y), whiteLevelCamera.Z);
                    var cameraWhiteMax = Math.Max(Math.Max(whiteLevelCamera.X, whiteLevelCamera.Y), whiteLevelCamera.Z);
                    Vector3 cameraWhite = whiteLevelCamera / cameraWhiteMin;
                    Vector3 cameraWhiteNormalised = whiteLevelCamera / cameraWhiteMax;
                    program.SetArgument(kernel, argumentIndex++, cameraWhite);
                    program.SetArgument(kernel, argumentIndex++, cameraWhiteNormalised);

                    // Set raw to display
                    program.SetArgument(kernel, argumentIndex++, cameraToDisplayColourMatrix);

                    // Highlight roll off
                    program.SetArgument(kernel, argumentIndex++, (int)clip.RawParameters.Value.highlightRollOff.GetValueOrDefault(HighlightRollOff.Medium));

                    // Gamut compression
                    program.SetArgument(kernel, argumentIndex++, (int)clip.RawParameters.Value.gamutCompression.GetValueOrDefault(GamutCompression.Rec709));
                }
                
                // Set linearise table+range
                if (metadata.LinearizationTable != null && metadata.LinearizationTable.Length > 0 && linearizeTable != null)
                {
                    program.SetArgument(kernel, argumentIndex++, linearizeTable);
                    var tableInputRange = (1 << (int)metadata.BitDepth) - 1;
                    program.SetArgument(kernel, argumentIndex++, (float)tableInputRange / (float)decodedMaxlevel);
                }

                // Lock GL texture output
                if ( output.Texture != null)
                    queue.AcquireTextureObject(renderContext, output);

                // Run the kernel 4 pixels at a time
                var launchDimensions = output.Dimensions / 2;
                var clipDisplayDimensions = metadata.DefaultCrop.HasValue ? metadata.DefaultCrop.Value.Zw : metadata.Dimensions;
                //Debug.Assert(clipDisplayDimensions == (launchDimensions * 2));
                program.Run2D(queue, kernel, launchDimensions);

                // Release access to GL texture
                if (output.Texture != null)
                    queue.ReleaseTextureObject(output);

                if (postProcessAction != null)
                    postProcessAction();
            };

            if (immediate)
                renderAction.Invoke();
            else
                renderContext.EnqueueRenderAction(renderAction);
            
            Processed = true;
            return Error.None;
        }

        private string ComputeKernelForClip(IO.DNG.MetadataCinemaDNG metadata, bool useLut)
        {
            string kernel = "Process";
            if (metadata.ColorProfile.HasValue)
                kernel += "Bayer";

            if (useLut)
                kernel += "LUT";
            
            return kernel;
        }
    }
}
