﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Octopus.Player.Core.Playback
{
    public abstract class Playback : IPlayback
    {
        public virtual uint FirstFrame { get { return 0; } }
        public virtual uint LastFrame { get { return Clip.Metadata.DurationFrames - 1; } }

        protected uint BufferDurationFrames { get; private set; }

        Timer FrameRequestTimer { get; set; }
        Timer FrameDisplayTimer { get; set; }

        private uint? requestFrame;
        private uint? displayFrame;

        public Playback(IPlayerWindow playerWindow, GPU.Render.IContext renderContext, uint bufferDurationFrames)
        {
            requestFrame = null;
            displayFrame = null;
            State = State.Empty;
            PlayerWindow = playerWindow;
            RenderContext = renderContext;
            BufferDurationFrames = bufferDurationFrames;
        }

        protected GPU.Render.IContext RenderContext { get; private set; }
        protected IPlayerWindow PlayerWindow { get; private set; }

        public virtual List<Essence> SupportedEssence { get; }
        private volatile State state;
        public State State
        {
            get { return state; }
            protected set
            {
                if (state != value)
                {
                    state = value;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler StateChanged;
        public IClip Clip { get; protected set; }
        public bool IsPlaying { get { return State == State.Playing || State == State.PlayingFromBuffer || State == State.Buffering; } }
        public bool IsPaused { get { return State == State.Paused || State == State.PausedEnd; } }

        public event EventHandler<uint> FrameDisplayed;

        public abstract void Close();
        public abstract Error Open(IClip clip);
        public bool IsOpen()
        {
            return Clip != null;
        }
        public abstract event EventHandler ClipOpened;
        public abstract event EventHandler ClipClosed;

        public abstract bool SupportsClip(IClip clip);
        public virtual void Stop()
        {
            Debug.Assert(State != State.Stopped && State != State.Empty);
            State = State.Stopped;

            // Dispose the frame request timer
            if (FrameRequestTimer != null)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    FrameRequestTimer.Dispose(waitHandle);
                    waitHandle.WaitOne();
                }
                FrameRequestTimer = null;
            }

            // Dispose the frame displaytimer
            if (FrameDisplayTimer != null)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    FrameDisplayTimer.Dispose(waitHandle);
                    waitHandle.WaitOne();
                }
                FrameDisplayTimer = null;
            }

            requestFrame = null;
            displayFrame = null;
        }

        public virtual void Play()
        {
            Trace.WriteLine("Playback::Play");

            // Stop first if we're paused at the end
            if (State == State.PausedEnd)
                Stop();

            Debug.Assert(State == State.Stopped || State == State.Paused);
            Debug.Assert(FrameRequestTimer == null && FrameDisplayTimer == null);

            // Resuming from paused, continue from the last displayed frame
            // Otherwise start from the first frame
            if (State == State.Paused)
                requestFrame = displayFrame;
            else if (!requestFrame.HasValue)
                requestFrame = FirstFrame;
            displayFrame = requestFrame;

            // Start frame request/display timer
            var frameDuration = TimeSpan.FromSeconds(1.0 / Clip.Metadata.Framerate.ToDouble());
            FrameRequestTimer = new Timer(new TimerCallback(OnFrameRequest), null, TimeSpan.Zero, frameDuration);
            FrameDisplayTimer = new Timer(new TimerCallback(OnFrameDisplay), null, TimeSpan.FromSeconds(BufferDurationFrames / Clip.Metadata.Framerate.ToDouble()), frameDuration);

            State = State.Buffering;
        }

        private void OnFrameRequest(object obj)
        {
            PlayerWindow.InvokeOnUIThread(() =>
            {
                if (!(State == State.Buffering || State == State.Playing))
                    return;

                RequestFrame(requestFrame.Value);

                // Last frame requested, we're now playing from the buffer
                if (requestFrame >= LastFrame)
                    State = State.PlayingFromBuffer;
                else if (requestFrame < LastFrame)
                    requestFrame++;
            });
        }

        private void OnFrameDisplay(object obj)
        {
            PlayerWindow.InvokeOnUIThread(() =>
            {
                if (!(State == State.Buffering || State == State.Playing || State == State.PlayingFromBuffer))
                    return;

                DisplayFrame(displayFrame.Value);
                FrameDisplayed?.Invoke(this, displayFrame.Value);

                // Last frame displayed, pause
                if (displayFrame >= LastFrame)
                    Pause();
                else if (displayFrame < LastFrame)
                    displayFrame++;

                // Buffering becomes playing when the first frame is displayed
                if (State == State.Buffering)
                    State = State.Playing;
            });
        }

        public virtual void Pause()
        {
            Trace.WriteLine("Playback::Pause");

            Debug.Assert(State == State.Playing || State == State.Buffering || State == State.PlayingFromBuffer);
            State = (displayFrame >= LastFrame) ? State.PausedEnd : State.Paused;

            // Dispose the frame request timer
            if (FrameRequestTimer != null)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    FrameRequestTimer.Dispose(waitHandle);
                    waitHandle.WaitOne();
                }
                FrameRequestTimer = null;
            }

            // Dispose the frame displaytimer
            if (FrameDisplayTimer != null)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    FrameDisplayTimer.Dispose(waitHandle);
                    waitHandle.WaitOne();
                }
                FrameDisplayTimer = null;
            }
        }

        public abstract Error RequestFrame(uint frameNumber);

        public abstract Error DisplayFrame(uint frameNumber);

        public abstract void OnRenderFrame(double timeInterval);

        public void Dispose()
        {
            if (IsOpen())
                Close();
        }
    }
}

