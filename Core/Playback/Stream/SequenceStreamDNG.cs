using Octopus.Player.Core.Playback.Stream;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Octopus.Player.Core.Playback
{
    public class SequenceStreamDNG : SequenceStream
    {
        private Core.IO.DNG.Reader DNGReader { get; set; }

        public SequenceStreamDNG(ClipCinemaDNG clip, GPU.Render.IContext gpuContext) 
            : base(clip, gpuContext, clip.Metadata.BitDepth > 8 ? GPU.Render.TextureFormat.R16 : GPU.Render.TextureFormat.R8, TimeSpan.FromSeconds(0.25f))
        {

        }

        public override void Dispose()
        {
            if (DNGReader != null)
            {
                DNGReader.Dispose();
                DNGReader = null;
            }
        }

        public override Error DecodeFrame(SequenceFrame frame)
        {
            // Cast to DNG clip/metadata
            var dngClip = (ClipCinemaDNG)Clip;
            var dngMetadata = (IO.DNG.MetadataCinemaDNG)dngClip.Metadata;
            Debug.Assert(dngClip != null && dngMetadata != null);

            // Sanity check the frame number is valid
            if (frame.frameNumber > dngMetadata.LastFrame || frame.frameNumber < dngMetadata.FirstFrame)
                return Error.BadFrameIndex;

            // Get and check the dng frame path
            string framePath;
            var getFrameResult = dngClip.GetFramePath(frame.frameNumber, out framePath);
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
            //DNGReader.Read
            //Clip.Path

            //throw new NotImplementedException();
            DNGReader.Dispose();
            DNGReader = null;
            return Error.NotImplmeneted;
        }
    }
}
