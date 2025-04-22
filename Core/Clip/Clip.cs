using Octopus.Player.Core.IO;
using Octopus.Player.Core.Playback;
using System;
using System.Collections.Generic;
namespace Octopus.Player.Core
{
	public abstract class Clip : IClip
	{
		public Clip()
		{
		}

        public abstract Audio.Codec AudioCodec { get; }
        public string Path { get; protected set; }
        public abstract Essence Essence { get; }
        public IMetadata Metadata { get; protected set; }
        public bool Valid { get; protected set; }
        public RawParameters? RawParameters { get; set; }
        public abstract IClip NextClip { get; }
        public abstract IClip PreviousClip { get; }

        public abstract Error ReadMetadata(uint? frame = null);
        public abstract Error Validate();

        public static IClip FromPath(string path, IList<SupportedFormat> supportedFormats, out Error error)
        {
            error = Error.None;

            // Do some checks on the supplied path
            bool isRegularFile = System.IO.File.Exists(path);
            bool isDir = System.IO.Directory.Exists(path);
            if (!isDir && !isRegularFile)
            {
                error = Error.BadPath;
                return null;
            }

            // Get path extension
            var pathExtension = System.IO.Path.GetExtension(path);

            // Try supported formats
            foreach (var format in supportedFormats)
            {
                var formatEssence = format.Container.Essence();
                var formatExtension = format.Container.Extension();

                switch (formatEssence)
                {
                    // Video essence should always be a regular file and match the format extension
                    case Essence.Video:
                        if (isRegularFile && formatExtension == pathExtension)
                        {
                            var clip = format.ClipFactory(path);
                            if (clip != null)
                            {
                                error = clip.Validate();
                                if (error == Error.None)
                                    return clip;
                            }
                        }
                        break;

                    case Essence.Sequence:

                        // Sequence essence and the supplied path is a regular file matching the container extension
                        if (!isDir && isRegularFile && formatExtension == pathExtension)
                        {
                            var folder = System.IO.Directory.GetParent(path).FullName;
                            var clip = format.ClipFactory(folder);
                            if (clip != null)
                            {
                                error = clip.Validate();
                                if (error == Error.None)
                                    return clip;
                            }
                        }

                        // Sequence essence and the supplied path is not a file, but a folder
                        if (isDir && !isRegularFile)
                        {
                            var clip = format.ClipFactory(path);
                            if (clip != null)
                            {
                                error = clip.Validate();
                                if (error == Error.None)
                                    return clip;
                            }
                        }
                        break;

                    default:
                        throw new System.Exception("Unknown essence type: " + formatEssence.ToString());
                }
            }

            if (error == Error.None)
                error = Error.UnsupportedFormat;

            return null;
        }

        public abstract IPlayback CreatePlayback(IPlayerWindow window, GPU.Compute.IContext computeContext, GPU.Render.IContext renderContext);
    }
}

