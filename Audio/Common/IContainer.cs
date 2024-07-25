using System;

namespace Octopus.Player.Audio
{
	public enum Codec
	{
		None,
		Wav
	}

	public interface IContainer
	{
		string Path { get; }
		Codec AudioCodec { get; }
	}
}