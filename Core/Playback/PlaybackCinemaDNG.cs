using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.Playback
{
	public class PlaybackCinemaDNG : Playback
	{
        private static readonly uint bufferDurationFrames = 8;

        private ISequenceStream SequenceStream { get; set; }
        private IShader GpuPipelineProgram { get; set; }
        private ITexture GpuFrameTest { get; set; }
        private ITexture LinearizeTableTest { get; set; }

        private SequenceFrame testFrame;

        public override event EventHandler ClipOpened;
        public override event EventHandler ClipClosed;

        public PlaybackCinemaDNG(GPU.Render.IContext renderContext)
            : base(renderContext, bufferDurationFrames)
        {

        }

        public override List<Essence> SupportedEssence { get { return new List<Essence>() { Essence.Sequence }; } }

        public override void Play()
        {
            // CinemaDNG might not start from frame index 0
            if (!displayFrame.HasValue)
                displayFrame = ((IO.DNG.MetadataCinemaDNG)Clip.Metadata).FirstFrame;
            if (!requestFrame.HasValue)
                requestFrame = ((IO.DNG.MetadataCinemaDNG)Clip.Metadata).FirstFrame;

            base.Play();
        }

        public override void Close()
        {
            if (State != State.Stopped)
                Stop();
            Debug.Assert(IsOpen() && SequenceStream != null);
            if (SequenceStream != null)
            {
                SequenceStream.Dispose();
                SequenceStream = null;
            }
            if (GpuFrameTest != null)
            {
                GpuFrameTest.Dispose();
                GpuFrameTest = null;
            }
            if(LinearizeTableTest != null)
            {
                LinearizeTableTest.Dispose();
                LinearizeTableTest = null;
            }
            if (testFrame.decodedImage != null)
                testFrame.Dispose();
            State = State.Empty;
            Clip = null;
            ClipClosed?.Invoke(this, new EventArgs());
        }

        public override Error Open(IClip clip)
        {
            Debug.Assert(State == State.Empty);
            Debug.Assert(!IsOpen());
            if (IsOpen())
                Close();

            // Load metadata, if that was unsuccesful, bail out
            var cinemaDNGClip = (ClipCinemaDNG)clip;
            Debug.Assert(cinemaDNGClip != null);
            if (clip.ReadMetadata() != Error.None)
                return Error.BadMetadata;
            Clip = clip;
            ClipOpened?.Invoke(this, new EventArgs());
            var cinemaDNGMetadata = (IO.DNG.MetadataCinemaDNG)cinemaDNGClip.Metadata;

            // Rebuild the shader if the defines have changed
            var requiredShaderDefines = ShaderDefinesForClip(clip);
            if ( GpuPipelineProgram == null || !requiredShaderDefines.ToHashSet().SetEquals(GpuPipelineProgram.Defines) )
            {
                if (GpuPipelineProgram != null)
                    GpuPipelineProgram.Dispose();
                GpuPipelineProgram = RenderContext.CreateShader(System.Reflection.Assembly.GetExecutingAssembly(), "PipelineCinemaDNG", "PipelineCinemaDNG", requiredShaderDefines);
            }

            // Create the sequence stream
            Debug.Assert(SequenceStream == null);
            var gpuFormat = clip.Metadata.BitDepth > 8 ? GPU.Render.TextureFormat.R16 : GPU.Render.TextureFormat.R8;
            SequenceStream = new SequenceStream<SequenceFrameDNG>((ClipCinemaDNG)clip, RenderContext, gpuFormat, BufferDurationFrames);

            // State is now stopped
            State = State.Stopped;

            // Decode test
            testFrame = new SequenceFrameDNG(RenderContext, clip, gpuFormat);
            testFrame.frameNumber = cinemaDNGMetadata.FirstFrame;
            testFrame.Decode(clip);

            // Test frame texture (Non tiled)
            if (GpuFrameTest != null)
                GpuFrameTest.Dispose();
            GpuFrameTest = RenderContext.CreateTexture(cinemaDNGClip.Metadata.Dimensions, clip.Metadata.DecodedBitDepth == 8 ? GPU.Render.TextureFormat.R8 : GPU.Render.TextureFormat.R16,
                cinemaDNGMetadata.TileCount == 0 ? testFrame.decodedImage : null, TextureFilter.Nearest, "gpuFrameTest");
            if (cinemaDNGMetadata.TileCount == 0)
                testFrame.Dispose();

            // Test linearse table
            if ( cinemaDNGMetadata.LinearizationTable != null && cinemaDNGMetadata.LinearizationTable.Length > 0 )
            {
                if (LinearizeTableTest != null)
                    LinearizeTableTest.Dispose();
                Span<byte> tableData = System.Runtime.InteropServices.MemoryMarshal.Cast<ushort, byte>(cinemaDNGMetadata.LinearizationTable);
                LinearizeTableTest = RenderContext.CreateTexture((uint)cinemaDNGMetadata.LinearizationTable.Length, GPU.Render.TextureFormat.R16, tableData.ToArray());
            }
            
            return Error.None;
        }

        private IList<string> ShaderDefinesForClip(IClip clip)
        {
            Debug.Assert(SupportsClip(clip));
            var dngMetadata = (IO.DNG.MetadataCinemaDNG)clip.Metadata;
            Debug.Assert(dngMetadata != null);

            var defines = new List<string>();

            // Always output Rec709 for now
            defines.Add("GAMMA_REC709");

            switch (dngMetadata.CFAPattern)
            {
                case IO.CFAPattern.None:
                    defines.Add("MONOCHROME");
                    break;
                case IO.CFAPattern.RGGB:
                    defines.Add("BAYER_XGGX");
                    defines.Add("BAYER_RB");
                    break;
                case IO.CFAPattern.BGGR:
                    defines.Add("BAYER_XGGX");
                    defines.Add("BAYER_BR");
                    break;
                case IO.CFAPattern.GBRG:
                    defines.Add("BAYER_GXXG");
                    defines.Add("BAYER_BR");
                    break;
                case IO.CFAPattern.GRBG:
                    defines.Add("BAYER_GXXG");
                    defines.Add("BAYER_RB");
                    break;
                default:
                    throw new Exception("Unsupported DNG CFA pattern");
            }

            if ( dngMetadata.LinearizationTable != null && dngMetadata.LinearizationTable.Length > 0 )
                defines.Add("LINEARIZE");

            return defines;
        }

        public override bool SupportsClip(IClip clip)
        {
            return (clip.GetType() == typeof(ClipCinemaDNG) && SupportedEssence.Contains(clip.Essence));
        }

        public override void OnRenderFrame(double timeInterval)
        {
            if (GpuPipelineProgram != null && GpuPipelineProgram.Valid && GpuFrameTest != null && GpuFrameTest.Valid && Clip != null)
            {
                // Tiled DNG frame test
                var cinemaDNGMetadata = (IO.DNG.MetadataCinemaDNG)Clip.Metadata;
                if (testFrame.decodedImage != null && cinemaDNGMetadata.TileCount > 0)
                {
                    var frameOffset = 0;
                    var tileSizeBytes = (cinemaDNGMetadata.TileDimensions.Area() * Clip.Metadata.DecodedBitDepth) / 8;
                    for (int y = 0; y < Clip.Metadata.Dimensions.Y; y += cinemaDNGMetadata.TileDimensions.Y)
                    {
                        for (int x = 0; x < Clip.Metadata.Dimensions.X; x += cinemaDNGMetadata.TileDimensions.X)
                        {
                            GpuFrameTest.Modify(RenderContext, new Vector2i(x, y), cinemaDNGMetadata.TileDimensions, testFrame.decodedImage, (uint)frameOffset);
                            frameOffset += (int)tileSizeBytes;
                        }
                    }
                    testFrame.Dispose();
                }

                // Calculate and apply exposure
                var exposure = Math.Pow(2.0, Clip.RawParameters.Value.exposure.HasValue ? Clip.RawParameters.Value.exposure.Value : cinemaDNGMetadata.ExposureValue );
                GpuPipelineProgram.SetUniform(RenderContext, "exposure", (float)exposure);

                // Calculate and apply black/white levels
                var blackWhiteLevel = new Vector2(cinemaDNGMetadata.BlackLevel, cinemaDNGMetadata.WhiteLevel);
                var decodedMaxlevel = (1 << (int)Clip.Metadata.DecodedBitDepth) - 1;
                GpuPipelineProgram.SetUniform(RenderContext, "blackWhiteLevel", blackWhiteLevel / (float)decodedMaxlevel);

                // Apply advanced raw parameters
                GpuPipelineProgram.SetUniform(RenderContext, "toneMappingOperator", (int)Clip.RawParameters.Value.toneMappingOperator.GetValueOrDefault(ToneMappingOperator.SDR));

                // Linearization table test
                if (cinemaDNGMetadata.LinearizationTable != null && cinemaDNGMetadata.LinearizationTable.Length > 0 )
                {
                    var tableInputRange = (1 << (int)cinemaDNGMetadata.BitDepth) - 1;
                    GpuPipelineProgram.SetUniform(RenderContext, "linearizeTableRange", (float)tableInputRange / (float)decodedMaxlevel);
                }

                if ( Clip.Metadata.ColorProfile.HasValue )
                {
                    var colorProfile = Clip.Metadata.ColorProfile.Value;

                    // Combine camera to xyz/xyz to display colour matrices
                    var cameraToXYZD50Matrix = colorProfile.CalculateCameraToXYZD50(Clip.RawParameters.Value.whiteBalance);
                    var xyzToDisplayColourMatrix = Maths.Color.Matrix.XYZToRec709D50();
                    var cameraToDisplayColourMatrix = Maths.Color.Matrix.NormalizeColourMatrix(xyzToDisplayColourMatrix) * cameraToXYZD50Matrix;
                    GpuPipelineProgram.SetUniform(RenderContext, "cameraToDisplayColour", cameraToDisplayColourMatrix);

                    // Calculate camera white in RAW space
                    var cameraToDisplayInv = Matrix3.Invert(cameraToDisplayColourMatrix);
                    var whiteLevelCamera = cameraToDisplayInv * Vector3.One;
                    var cameraWhiteMin = Math.Min(Math.Min(whiteLevelCamera.X, whiteLevelCamera.Y), whiteLevelCamera.Z);
                    var cameraWhiteMax = Math.Max(Math.Max(whiteLevelCamera.X, whiteLevelCamera.Y), whiteLevelCamera.Z);
                    Vector3 cameraWhite = whiteLevelCamera / cameraWhiteMin;
                    Vector3 cameraWhiteNormalised = whiteLevelCamera / cameraWhiteMax;
                    GpuPipelineProgram.SetUniform(RenderContext, "cameraWhite", cameraWhite);

                    // Calculate luminance weights for RAW by pushing the standard rec709 luminance weights back through inverted CamreaTo709
                    // Used for highlight rolloff
                    var cameraTo709Inv = Matrix3.Invert(Maths.Color.Matrix.XYZToRec709D50() * cameraToXYZD50Matrix);
                    var luminanceWeightUnormalised = cameraTo709Inv * Maths.Color.Profile.Rec709LuminanceWeights;
                    Vector3 RAWLuminanceWeight = luminanceWeightUnormalised / (luminanceWeightUnormalised.X + luminanceWeightUnormalised.Y + luminanceWeightUnormalised.Z);
                    GpuPipelineProgram.SetUniform(RenderContext, "cameraWhiteNormalised", cameraWhiteNormalised);
                    GpuPipelineProgram.SetUniform(RenderContext, "rawLuminanceWeight", RAWLuminanceWeight);

                    // Apply advanced raw parameters
                    GpuPipelineProgram.SetUniform(RenderContext, "highlightRecovery", (int)Clip.RawParameters.Value.highlightRecovery.GetValueOrDefault(HighlightRecovery.On));
                    GpuPipelineProgram.SetUniform(RenderContext, "highlightRollOff", (int)Clip.RawParameters.Value.highlightRollOff.GetValueOrDefault(HighlightRollOff.Low));
                    GpuPipelineProgram.SetUniform(RenderContext, "gamutCompression",(int)Clip.RawParameters.Value.gamutCompression.GetValueOrDefault(GamutCompression.Rec709));
                }
                Vector2i rectPos;
                Vector2i rectSize;
                RenderContext.FramebufferSize.FitAspectRatio(Clip.Metadata.AspectRatio, out rectPos, out rectSize);
                var textures = new Dictionary<string, ITexture> { { "rawImage", GpuFrameTest } };
                if (LinearizeTableTest != null)
                    textures["linearizeTable"] = LinearizeTableTest;
                RenderContext.Draw2D(GpuPipelineProgram, textures, rectPos, rectSize);
            }
        }

        public override Error RequestFrame(uint frameNumber)
        {
            Trace.WriteLine("Request frame: " + frameNumber);
            return Error.NotImplmeneted;
        }

        public override Error DisplayFrame(uint frameNumber)
        {
            Trace.WriteLine("Display frame: " + frameNumber);

            // Trigger a redraw
            //RenderContext.RequestRender();

            return Error.NotImplmeneted;
        }
    }
}

