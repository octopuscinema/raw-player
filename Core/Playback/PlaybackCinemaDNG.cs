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
            // Load GPU program for CinemaDNG pipeline
            var defaultShaderDefines = new List<string>() { "BAYER_XGGX", "BAYER_RB" };
            GpuPipelineProgram = renderContext.CreateShader(System.Reflection.Assembly.GetExecutingAssembly(), "PipelineCinemaDNG", "PipelineCinemaDNG", defaultShaderDefines);
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
            if ( GpuPipelineProgram != null && !requiredShaderDefines.ToHashSet().SetEquals(GpuPipelineProgram.Defines) )
            {
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
                Vector2i rectPos;
                Vector2i rectSize;
                RenderContext.FramebufferSize.FitAspectRatio(Clip.Metadata.AspectRatio, out rectPos, out rectSize);
                RenderContext.Draw2D(GpuPipelineProgram, new Dictionary<string, ITexture> { { "rawImage", GpuFrameTest } }, rectPos, rectSize);
            }
        }
    }
}

