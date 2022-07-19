using System;
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

        protected uint? requestFrame;
        protected uint? displayFrame;
        protected Mutex playbackControlMutex;

        public Playback(GPU.Render.IContext renderContext, uint bufferDurationFrames)
        {
            requestFrame = null;
            displayFrame = null;
            State = State.Empty;
            RenderContext = renderContext;
            BufferDurationFrames = bufferDurationFrames;
            playbackControlMutex = new Mutex();
        }

        protected GPU.Render.IContext RenderContext { get; private set; }

        public virtual List<Essence> SupportedEssence { get; }
        private State state;
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
        public bool IsPlaying { get { return State == State.Playing; } }
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
        public void Stop()
        {
            try
            {
                playbackControlMutex.WaitOne();
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
            finally
            {
                playbackControlMutex.ReleaseMutex();
            }
        }

        public virtual void Play()
        {
            // Stop first if we're paused at the end
            if (State == State.PausedEnd)
                Stop();

            try
            {
                playbackControlMutex.WaitOne();
                Debug.Assert(State == State.Stopped || State == State.Paused);
                Debug.Assert(FrameRequestTimer == null && FrameDisplayTimer == null);

                // Set starting frame if necessary
                if (!requestFrame.HasValue)
                    requestFrame = FirstFrame;
                displayFrame = requestFrame;

                // Start frame request/display timer
                var frameDuration = TimeSpan.FromSeconds(1.0 / Clip.Metadata.Framerate.ToDouble());
                FrameRequestTimer = new Timer(new TimerCallback(OnFrameRequest), null, TimeSpan.Zero, frameDuration);
                FrameDisplayTimer = new Timer(new TimerCallback(OnFrameDisplay), null, TimeSpan.FromSeconds(BufferDurationFrames / Clip.Metadata.Framerate.ToDouble()), frameDuration);

                State = State.Playing;
            }
            finally
            {
                playbackControlMutex.ReleaseMutex();
            }
        }

        private void OnFrameRequest(object obj)
        {
            if (State != State.Playing)
                return;

            RequestFrame(requestFrame.Value);

            // Last frame requested, disable the timer and set state to paused at end
            if (requestFrame >= LastFrame)
            {
                try
                {
                    playbackControlMutex.WaitOne();
                    FrameRequestTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    State = State.PausedEnd;
                }
                finally
                {
                    playbackControlMutex.ReleaseMutex();
                }
            }
            else
                requestFrame++;
        }

        private void OnFrameDisplay(object obj)
        {
            if (State == State.Playing || (State == State.PausedEnd && displayFrame <= LastFrame))
            {
                DisplayFrame(displayFrame.Value);
                FrameDisplayed?.Invoke(this, displayFrame.Value);

                // Last frame displayed, disable the frame display timer
                if (displayFrame >= LastFrame)
                {
                    try
                    {
                        playbackControlMutex.WaitOne();
                        FrameDisplayTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                    finally
                    {
                        playbackControlMutex.ReleaseMutex();
                    }
                }
                else
                    displayFrame++;
            }
        }

        public void Pause()
        {
            try
            {
                playbackControlMutex.WaitOne();
                Debug.Assert(State == State.Playing);
                State = State.Paused;

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
            finally 
            { 
                playbackControlMutex.ReleaseMutex(); 
            }
        }

        public abstract Error RequestFrame(uint frameNumber);

        public abstract Error DisplayFrame(uint frameNumber);

        public abstract void OnRenderFrame(double timeInterval);

        public void Dispose()
        {
            if (IsOpen())
                Close();
            playbackControlMutex.Dispose();
            playbackControlMutex = null;
        }
    }
}

