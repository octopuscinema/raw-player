using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Octopus.Player.Core.Playback
{
	public abstract class Playback : IPlayback
	{
        protected Timer FrameRequestTimer { get; private set; }
        protected Timer FrameDisplayTimer { get; private set; }
        protected uint BufferDurationFrames { get; private set; }

        protected uint? requestFrame;
        protected uint? displayFrame;

        public Playback(GPU.Render.IContext renderContext, uint bufferDurationFrames)
		{
            requestFrame = null;
            displayFrame = null;
            State = State.Empty;
            RenderContext = renderContext;
            BufferDurationFrames = bufferDurationFrames;
        }

        ~Playback()
        {
            Debug.Assert(!IsOpen());
            if (IsOpen())
                Close();
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
        public bool IsPaused { get { return State == State.Paused; } }

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
            Debug.Assert(State != State.Stopped && State != State.Empty);

            if (FrameRequestTimer != null)
                FrameRequestTimer.Dispose();
            FrameRequestTimer = null;
            if (FrameDisplayTimer != null)
                FrameDisplayTimer.Dispose();
            FrameDisplayTimer = null;

            requestFrame = null;
            displayFrame = null;
            State = State.Stopped;
        }

        public virtual void Play()
        {
            Debug.Assert(State == State.Stopped || State == State.Paused);
            Debug.Assert(FrameRequestTimer == null && FrameDisplayTimer == null);

            // Set starting frame if necessary
            // Video formats which don't start from frame index '0' should override Play() and set this.requestFrame before base.Play()
            if (!requestFrame.HasValue)
                requestFrame = 0;
            displayFrame = requestFrame;

            // Start frame request/display timer
            var frameDuration = TimeSpan.FromSeconds(1.0 / Clip.Metadata.Framerate.ToDouble());
            FrameRequestTimer = new Timer(new TimerCallback(OnFrameRequest), null, TimeSpan.Zero, frameDuration);
            FrameDisplayTimer = new Timer(new TimerCallback(OnFrameDisplay), null, TimeSpan.FromSeconds(BufferDurationFrames / Clip.Metadata.Framerate.ToDouble()), frameDuration);
            State = State.Playing;
        }

        private void OnFrameRequest(object obj)
        {
            RequestFrame(requestFrame.Value);
            requestFrame++;
        }

        private void OnFrameDisplay(object obj)
        {
            DisplayFrame(displayFrame.Value);
            displayFrame++;
        }

        public void Pause()
        {
            Debug.Assert(State == State.Playing);

            FrameRequestTimer.Dispose();
            FrameRequestTimer = null;

            FrameDisplayTimer.Dispose();
            FrameDisplayTimer = null;

            State = State.Paused;
        }

        public abstract Error RequestFrame(uint frameNumber);

        public abstract Error DisplayFrame(uint frameNumber);

        public abstract void OnRenderFrame(double timeInterval);
    }
}

