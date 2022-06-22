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
        private SequenceStreamDNG SequenceStreamDNG { get; set; }
        private IShader GpuPipelineProgram { get; set; }
        private ITexture GpuFrameTest { get; set; }

        public override event EventHandler ClipOpened;
        public override event EventHandler ClipClosed;

        public PlaybackCinemaDNG(GPU.Render.IContext renderContext)
            : base(renderContext)
        {

        }

        public override List<Essence> SupportedEssence { get { return new List<Essence>() { Essence.Sequence }; } }

        public override void Close()
        {
            Debug.Assert(IsOpen() && SequenceStreamDNG != null);
            if (SequenceStreamDNG != null)
            {
                SequenceStreamDNG.Dispose();
                SequenceStreamDNG = null;
            }
            if (GpuFrameTest != null)
            {
                GpuFrameTest.Dispose();
                GpuFrameTest = null;
            }
            State = State.Empty;
            Clip = null;
            ClipClosed?.Invoke(this, new EventArgs());
        }

        public override Error Open(IClip clip)
        {
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

            // Rebuild the shader if the defines have changed
            var requiredShaderDefines = ShaderDefinesForClip(clip);
            if ( GpuPipelineProgram == null || !requiredShaderDefines.ToHashSet().SetEquals(GpuPipelineProgram.Defines) )
            {
                if (GpuPipelineProgram != null)
                    GpuPipelineProgram.Dispose();
                GpuPipelineProgram = RenderContext.CreateShader(System.Reflection.Assembly.GetExecutingAssembly(), "PipelineCinemaDNG", "PipelineCinemaDNG", requiredShaderDefines);
            }

            // Create the sequence stream
            Debug.Assert(SequenceStreamDNG == null);
            SequenceStreamDNG = new SequenceStreamDNG((ClipCinemaDNG)clip, RenderContext);

            // Decode test
            var frame = new Stream.SequenceFrame(RenderContext, clip, clip.Metadata.DecodedBitDepth == 8 ? GPU.Render.TextureFormat.R8 : GPU.Render.TextureFormat.R16);
            frame.frameNumber = 0;
            SequenceStreamDNG.DecodeFrame(frame);

            // Test frame texture
            if (GpuFrameTest != null)
                GpuFrameTest.Dispose();
            GpuFrameTest = RenderContext.CreateTexture(cinemaDNGClip.Metadata.Dimensions, TextureFormat.R16, frame.decodedImage, TextureFilter.Nearest, "gpuFrameTest");

            return Error.NotImplmeneted;
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

            return defines;
        }

        public override bool SupportsClip(IClip clip)
        {
            return (clip.GetType() == typeof(ClipCinemaDNG) && SupportedEssence.Contains(clip.Essence));
        }

        public override void Stop()
        {
            throw new NotImplementedException();
        }

        public override void Play()
        {
            throw new NotImplementedException();
        }

        public override void Pause()
        {
            throw new NotImplementedException();
        }

        public override void OnRenderFrame(double timeInterval)
        {
            if (GpuPipelineProgram != null && GpuPipelineProgram.Valid && GpuFrameTest != null && GpuFrameTest.Valid && Clip != null)
            {
                if ( Clip.Metadata.ColorProfile.HasValue )
                {
                    var colorProfile = Clip.Metadata.ColorProfile.Value;

                    // Combine camera to xyz/xyz to display colour matrices
                    var cameraToXYZD50Matrix = colorProfile.CalculateCameraToXYZD50();
                    var xyzToDisplayColourMatrix = Maths.Color.Matrix.XYZToRec709D50();
                    var cameraToDisplayColourMatrix = Maths.Color.Matrix.NormalizeColourMatrix(xyzToDisplayColourMatrix) * cameraToXYZD50Matrix;
                    GpuPipelineProgram.SetUniform(RenderContext, "cameraToDisplayColour", cameraToDisplayColourMatrix);

                    var blackWhiteLevel = new Vector2(((IO.DNG.MetadataCinemaDNG)Clip.Metadata).BlackLevel,
                         ((IO.DNG.MetadataCinemaDNG)Clip.Metadata).WhiteLevel);
                    var decodedMaxlevel = (1 << (int)Clip.Metadata.DecodedBitDepth) - 1;
                    GpuPipelineProgram.SetUniform(RenderContext, "blackWhiteLevel", blackWhiteLevel/(float)decodedMaxlevel );
/*
                    // Calculate camera white in RAW space
#if HIGHLIGHT_RECOVERY_WB
                    const auto&CameraToXYZD50Inv = PM::Matrix3x3::Invert(CameraToXYZD50Matrix);
                    const auto&WhiteLevelCamera = CameraToXYZD50Inv * CJ3Vector3(1.0f, 1.0f, 1.0f);
#else
                    const auto CameraToDisplayInv = PM::Matrix3x3::Invert(CameraToReviewColourMatrix);
                    const auto&WhiteLevelCamera = CameraToDisplayInv * CJ3Vector3(1.0f, 1.0f, 1.0f);
#endif
                    const auto CameraWhiteMin = std::min(std::min(WhiteLevelCamera.X(), WhiteLevelCamera.Y()), WhiteLevelCamera.Z());
                    const auto CameraWhiteMax = std::max(std::max(WhiteLevelCamera.X(), WhiteLevelCamera.Y()), WhiteLevelCamera.Z());
                    const CJ3Vector3&CameraWhite = WhiteLevelCamera / CameraWhiteMin;
                    const CJ3Vector3&CameraWhiteNormalised = WhiteLevelCamera / CameraWhiteMax;

                    // Calculate luminance weights for RAW by pushing the standard rec709 luminance weights back through inverted CamreaTo709
                    const auto&CameraTo709Inv = PM::Matrix3x3::Invert(PF::DNG::XYZToRec709D50() * CameraToXYZD50Matrix);
                    const auto&LuminanceWeightUnormalised = CameraTo709Inv * PF::DNG::Rec709LuminanceWeights();
                    const CJ3Vector3&RAWLuminanceWeight = LuminanceWeightUnormalised / (LuminanceWeightUnormalised.m_X + LuminanceWeightUnormalised.m_Y + LuminanceWeightUnormalised.m_Z);

                    // Send highlight recovery uniforms to shader
                    pOutputShader->SetUniformData(CJ3PixelShader::UNIFORM_CUSTOM7, CameraWhite);
                    pOutputShader->SetUniformData(CJ3PixelShader::UNIFORM_CUSTOM8, CameraWhiteNormalised);
                    pOutputShader->SetUniformData(CJ3PixelShader::UNIFORM_CUSTOM9, RAWLuminanceWeight);

                    // Send linearise log base
                    float LineariseLogBase = m_pItem->MetaData().LineariseLogBase.has_value() ? *m_pItem->MetaData().LineariseLogBase : 0.0f;
                    pOutputShader->SetUniformData(CJ3PixelShader::UNIFORM_CUSTOM10, LineariseLogBase);

                    // Enable/disable gamut compression
                    const bool GamutCompression = (Capture::LUT::GamutCompression(pGammaLUT) == Capture::LUT::eGamutCompression::On);
                    pOutputShader->SetUniformData(CJ3PixelShader::UNIFORM_CUSTOM11, GamutCompression ? 1.0f : 0.0f);

                    // Set highlight/shadow rolloff
                    const CJ3Vector2I HighlightShadowRollOff((int) m_pItem->MetaData().HighlightRollOff, (int) m_pItem->MetaData().ShadowRollOff);
                    pOutputShader->SetUniformData(CJ3PixelShader::UNIFORM_CUSTOM12, (CJ3Vector2)HighlightShadowRollOff);
*/
                }
  
                Vector2i rectPos;
                Vector2i rectSize;
                RenderContext.FramebufferSize.FitAspectRatio(Clip.Metadata.AspectRatio, out rectPos, out rectSize);
                RenderContext.Draw2D(GpuPipelineProgram, new Dictionary<string, ITexture> { { "rawImage", GpuFrameTest } }, rectPos, rectSize);
            }
        }
    }
}

