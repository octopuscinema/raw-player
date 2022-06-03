using System;
namespace Octopus.Player.Core.Playback
{
	public interface IClip
	{
		string Path { get; }
		Essence Essence { get; }
		bool Valid { get; }

		Error Validate();

		Error ReadMetadata(uint? frame = null);
        IO.IMetadata Metadata { get; }
	}
}

