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
		PlayingFromBuffer,
		Paused,
		PausedEnd,
		PausedSeeking,
		End
	};

	public interface IPlayback : IDisposable
	{
		uint FirstFrame { get; }
		uint LastFrame { get; }
		Maths.Rational Framerate { get; }

		List<Essence> SupportedEssence { get; }
		State State { get; }
		IClip Clip { get; }
		event EventHandler StateChanged;

		// Playback controls
		void Stop();
		void Play();
		void Pause();
		bool IsPlaying { get; }
		bool IsPaused { get; }
		public delegate void FrameDisplayedEventHandler(uint frame, Maths.TimeCode timeCode);
		event FrameDisplayedEventHandler FrameDisplayed;

		// Clip control
		Error Open(IClip clip);
		bool IsOpen();
		void Close();
		bool SupportsClip(IClip clip);
		event EventHandler ClipOpened;
		event EventHandler ClipClosed;

		void OnRenderFrame(double timeInterval);
	}
}

