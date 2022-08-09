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

	public enum PlaybackVelocity
    {
		Backward10x = -10,
		Backward5x = -5,
		Backward2x = -2,
		Forward1x = 1,
		Forward2x = 2,
		Forward5x = 5,
		Forward10x = 10
	};

	public static class Extensions
    {
		public static bool IsForward(this PlaybackVelocity velocity)
		{
			return (int)velocity > 0;
		}
	}

	public interface IPlayback : IDisposable
	{
		uint FirstFrame { get; }
		uint LastFrame { get; }
		Maths.Rational Framerate { get; }

		List<Essence> SupportedEssence { get; }
		State State { get; }
		IClip Clip { get; }
		event EventHandler StateChanged;

		// Seek controls
		State? PreSeekState { get; }
		void SeekStart();
		Error RequestSeek(uint frame);
		void SeekEnd();
		uint? ActiveSeekRequest { get; }

		// Playback controls
		void Stop();
		void Play();
		void Pause();
		bool IsPlaying { get; }
		bool IsPaused { get; }
		bool IsSeeking { get; }
		PlaybackVelocity Velocity { get; set; }
		event EventHandler VelocityChanged;

		public delegate void FrameDisplayedEventHandler(uint frame, in Maths.TimeCode timeCode);
		public delegate void FrameSkippedEventHandler(uint requestedFrame, uint displayedFrame, in Maths.TimeCode synthesisedTimeCode);
		public delegate void FrameMissingEventHandler(uint requestedFrame, in Maths.TimeCode synthesisedTimeCode);
		event FrameDisplayedEventHandler FrameDisplayed;
		event FrameSkippedEventHandler FrameSkipped;
		event FrameMissingEventHandler FrameMissing;

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

