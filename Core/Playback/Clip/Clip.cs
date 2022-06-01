using Octopus.Player.Core.IO;
using System;
namespace Octopus.Player.Core.Playback
{
	public abstract class Clip : IClip
	{
		public Clip()
		{

		}

        public string Path { get; protected set; }
        public abstract Essence Essence { get; }
        public IMetadata Metadata { get; private set; }

        public abstract Error ReadMetadata();
        public abstract Error Validate();
    }
}

