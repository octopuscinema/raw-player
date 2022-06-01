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

        public abstract Error Validate();
    }
}

