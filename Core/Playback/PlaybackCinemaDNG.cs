using System;
using System.Collections.Generic;
using System.Diagnostics;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.Playback
{
	public class PlaybackCinemaDNG : Playback
	{
        private SequenceStreamDNG SequenceStreamDNG { get; set; }
        private IShader GpuPipelineProgram { get; set; }
        private ITexture GpuFrameTest { get; set; }

		public PlaybackCinemaDNG(GPU.Render.IContext renderContext)
            : base(renderContext)
		{
            //var testDll = Decoders.LJ92.TestMethod(1);

            // Load GPU program for CinemaDNG pipeline
            GpuPipelineProgram = renderContext.CreateShader(System.Reflection.Assembly.GetExecutingAssembly(), "PipelineCinemaDNG", "PipelineCinemaDNG");
            renderContext.RequestRender();
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
            State = State.Empty;
            Clip = null;
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

            // Test frame texture
            if (GpuFrameTest != null)
                GpuFrameTest.Dispose();
            GpuFrameTest = RenderContext.CreateTexture(cinemaDNGClip.Metadata.Dimensions, TextureFormat.R16, "gpuFrameTest");

            // Create the sequence stream
            Debug.Assert(SequenceStreamDNG == null);
            SequenceStreamDNG = new SequenceStreamDNG((ClipCinemaDNG)clip, RenderContext);

            // Wip decode test
            var frame = new Stream.SequenceFrame(RenderContext, clip, clip.Metadata.DecodedBitDepth == 8 ? GPU.Render.TextureFormat.R8 : GPU.Render.TextureFormat.R16);
            frame.frameNumber = 0;
            SequenceStreamDNG.DecodeFrame(frame);

            return Error.NotImplmeneted;
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
            if (GpuPipelineProgram != null)
                RenderContext.Draw2D(GpuPipelineProgram, null, new Vector2i(0, 0), new Vector2i(100, 100));
        }
    }
}

