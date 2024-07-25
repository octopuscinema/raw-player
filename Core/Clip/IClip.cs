using System;

namespace Octopus.Player.Core
{
	public interface IClip : Audio.IContainer
	{
		Essence Essence { get; }
		bool Valid { get; }

		Error Validate();

		Error ReadMetadata(uint? frame = null);
		IO.IMetadata Metadata { get; }

		RawParameters? RawParameters { get; set; }
		IClip NextClip { get; }
		IClip PreviousClip { get; }
	}
}

