using Octopus.Player.Core.IO;
using System;
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
    }
}

