using System;
namespace Octopus.Player.Core.Playback
{
	public interface IClip
	{
		string Path { get; }
		Essence Essence { get; }

		Error Validate();
	}
}

