using Octopus.Player.Core.Maths;
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

        public SequenceFrameDNG(IClip clip, GPU.Format format)
            : base(clip, format)
        {

        }

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
                    if (decodedImageGpu != null)
                    {
                        Debug.Assert(decodedImageGpu.Dimensions == clip.Metadata.PaddedDimensions);
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
                    }

                    // Decode to CPU buffer
                    else
                    {
                        Debug.Assert(decodedImageCpu != null && decodedImageCpu.Length == bytesPerPixel * clip.Metadata.PaddedDimensions.Area());
                        decodeDataError = DNGReader.DecodeImageData(decodedImageCpu, workingBuffer);
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
            if (result != Error.None)
                System.Runtime.CompilerServices.Unsafe.InitBlock(ref decodedImageCpu[0], 0, (uint)decodedImageCpu.Length);
            LastError = result;
            NeedsGPUCopy = true;
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

        public override Error CopyToGPU(IClip clip, IContext renderContext, ITexture gpuImage, byte[] stagingImage, bool immediate = false, Action postCopyAction = null)
        {
            // Copy to staging array if supplied
            if (stagingImage != null)
            {
                Debug.Assert(decodedImageCpu.Length == stagingImage.Length);
                Buffer.BlockCopy(decodedImageCpu, 0, stagingImage, 0, stagingImage.Length);
            }
            else
                stagingImage = decodedImageCpu;

            Action renderAction = () =>
            {
                // Modify the texture linear or tiled
                var metadata = (IO.DNG.MetadataCinemaDNG)clip.Metadata;
                if (metadata.TileCount > 0)
                    ForEachTile(metadata, (origin, size, offset) => { gpuImage.Modify(renderContext, origin, size, stagingImage, offset); });
                else
                    gpuImage.Modify(renderContext, Vector2i.Zero, gpuImage.Dimensions, stagingImage);

                if (postCopyAction != null)
                    postCopyAction();
            };

            if (immediate)
                renderAction.Invoke();
            else
                renderContext.EnqueueRenderAction(renderAction);

            NeedsGPUCopy = false;
            return Error.None;
        }

        public override Error Process(IClip clip, GPU.Compute.IImage2D output, GPU.Compute.IProgram program)
        {
            //program.SetArgument()
            //program.
            //ComputeQueue.
            return Error.NotImplmeneted;
        }
    }
}
