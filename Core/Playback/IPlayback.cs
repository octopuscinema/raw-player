using System;
using System.Collections.Generic;

namespace Octopus.Player.Core.Playback
{
	public enum State
	{
		Empty,
		Stopped,
		Buffering,
		Playing,
		Paused,
		PausedSeeking,
		End
	};

	public interface IPlayback : IDisposable
	{
		List<Essence> SupportedEssence { get; }
		State State { get; }
		IClip Clip { get; }

//#error TODO: change string to IClip and add a Clip.LocateClip static function
		Error Open(IClip clip);
		void Close();
	}
}

