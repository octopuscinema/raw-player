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

		public PlaybackCinemaDNG(GPU.Render.IContext renderContext)
            : base(renderContext)
		{
            // Load GPU program for CinemaDNG pipeline
            GpuPipelineProgram = renderContext.CreateShader(System.Reflection.Assembly.GetExecutingAssembly(), "PipelineCinemaDNG", "PipelineCinemaDNG");

            //var textureTest = renderContext.CreateTexture(new Vector2i(1920, 1080), TextureFormat.R16);
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
            /*
                        try
                        {
                            var dngFiles = System.IO.Directory.EnumerateFiles(Path, "*.dng", System.IO.SearchOption.TopDirectoryOnly);
                            return dngFiles.Any() ? Error.None : Error.NoVideoStream;
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine("Failed to validate CinemaDNG sequence path: " + Path + "\n" + e.Message);
                            return Error.BadPath;
                        }
            */

            // Test texture creation
            //var tex = RenderContext.CreateTexture(new Vector2i(0, 0), GPU.Render.TextureFormat.R16);

            // Create the sequence stream
            Debug.Assert(SequenceStreamDNG == null);
            SequenceStreamDNG = new SequenceStreamDNG((ClipCinemaDNG)clip);
               
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
    }
}

