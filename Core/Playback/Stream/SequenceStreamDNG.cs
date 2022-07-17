﻿using Octopus.Player.Core.Maths;
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

        public SequenceStreamDNG(ClipCinemaDNG clip, GPU.Render.IContext gpuContext, uint bufferDurationFrames = 8) 
            : base(clip, gpuContext, clip.Metadata.BitDepth > 8 ? GPU.Render.TextureFormat.R16 : GPU.Render.TextureFormat.R8, bufferDurationFrames)
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

        public override FrameRequestResult RequestFrame(uint frameNumber)
        {
            // Sanity check range
            var dngMetadata = (IO.DNG.MetadataCinemaDNG)Clip.Metadata;
            if (frameNumber < dngMetadata.FirstFrame || frameNumber > dngMetadata.LastFrame)
                return FrameRequestResult.ErrorFrameOutOfRange;

            return base.RequestFrame(frameNumber);
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

            // Read the data
            switch (DNGReader.Compression)
            {
                case IO.DNG.Compression.None:
                case IO.DNG.Compression.LosslessJPEG:
                    var bytesPerPixel = Clip.Metadata.BitDepth <= 8 ? 1 : 2;
                    Debug.Assert(frame.decodedImage.Length == bytesPerPixel * Clip.Metadata.Dimensions.Area());
                    DNGReader.DecodeImageData(frame.decodedImage);
                    break;
                default:
                    DNGReader.Dispose();
                    DNGReader = null;
                    return Error.NotImplmeneted;
            }

            // Done
            DNGReader.Dispose();
            DNGReader = null;
            return Error.None;
        }
    }
}
