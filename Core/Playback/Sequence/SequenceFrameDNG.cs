using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.IO;

namespace Octopus.Player.Core.Playback
{
    public class SequenceFrameDNG : SequenceFrame
    {
        private Core.IO.DNG.Reader DNGReader { get; set; }

        public SequenceFrameDNG(IContext gpuContext, IClip clip, GPU.Render.TextureFormat gpuFormat)
            : base(gpuContext, clip, gpuFormat)
        {

        }

        public override Error Decode(IClip clip)
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

            // Read the data
            switch (DNGReader.Compression)
            {
                case IO.DNG.Compression.None:
                case IO.DNG.Compression.LosslessJPEG:
                    var bytesPerPixel = clip.Metadata.BitDepth <= 8 ? 1 : 2;
                    Debug.Assert(decodedImage.Length == bytesPerPixel * clip.Metadata.Dimensions.Area());
                    DNGReader.DecodeImageData(decodedImage);
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

        public override Error CopyToGPU(IClip clip, IContext renderContext, ITexture gpuImage, byte[] stagingImage)
        {
            // Copy to staging array
            Debug.Assert(decodedImage.Length == stagingImage.Length);
            decodedImage.CopyTo(stagingImage, 0);

            renderContext.EnqueueRenderAction(() =>
            {

                // Tiled DNG
                var cinemaDNGMetadata = (IO.DNG.MetadataCinemaDNG)clip.Metadata;
                if (cinemaDNGMetadata.TileCount > 0)
                {
                    var frameOffset = 0;
                    var tileSizeBytes = (cinemaDNGMetadata.TileDimensions.Area() * clip.Metadata.DecodedBitDepth) / 8;
                    for (int y = 0; y < clip.Metadata.Dimensions.Y; y += cinemaDNGMetadata.TileDimensions.Y)
                    {
                        for (int x = 0; x < clip.Metadata.Dimensions.X; x += cinemaDNGMetadata.TileDimensions.X)
                        {
                            gpuImage.Modify(renderContext, new Vector2i(x, y), cinemaDNGMetadata.TileDimensions, stagingImage, (uint)frameOffset);
                            frameOffset += (int)tileSizeBytes;
                        }
                    }
                }
                else
                    gpuImage.Modify(renderContext, Vector2i.Zero, gpuImage.Dimensions, stagingImage);
            });

            return Error.None;
        }
    }
}
