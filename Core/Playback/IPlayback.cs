﻿using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

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

	public readonly struct ExportedFrame
	{
		public byte[] Data { get; }
		public Vector2i Dimensions { get; }
		public GPU.Format Format { get; }
		public GPU.Orientation Orientation { get; }
		public uint FrameNumber { get; }

		public ExportedFrame(byte[] data, in Vector2i dimensions, GPU.Format format, GPU.Orientation orientation, uint frameNumber)
		{
			Data = data;
			Dimensions = dimensions;
			Format = format;
			Orientation = orientation;
			FrameNumber = frameNumber;
		}
    }

	public interface IPlayback : IDisposable
	{
		uint FirstFrame { get; }
		uint LastFrame { get; }
		Maths.Rational Framerate { get; }
		uint? LastDisplayedFrame { get; }

        List<Essence> SupportedEssence { get; }
		State State { get; }
		IClip Clip { get; }
		event EventHandler StateChanged;

		// Seek controls
		State? PreSeekState { get; }
		void SeekStart();
		Error RequestSeek(uint frame, bool force = false);
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
        event FrameDisplayedEventHandler SeekFrameDisplayed;
        event FrameMissingEventHandler SeekFrameMissing;

		// Audio
		bool HasAudio { get; }
		void Mute();
		void Unmute();

        // LUT
        Error ApplyLUT(string resourceName);
        Error ApplyLUT(Uri path);
		void RemoveLUT();

        // Clip control
        Error Open(IClip clip);
		bool IsOpen();
		void Close();
		bool SupportsClip(IClip clip);
		event EventHandler ClipOpened;
		event EventHandler ClipClosed;

        // Export
        Error ExportFrame(out ExportedFrame frame, uint? frameNumber = null);

        void OnRenderFrame(double timeInterval);
	}
}

