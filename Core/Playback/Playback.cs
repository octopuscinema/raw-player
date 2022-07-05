using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace Octopus.Player.Core.Playback
{
	public abstract class Playback : IPlayback
	{
        protected Timer FrameTimer { get; private set; }
        
        static readonly protected uint bufferDurationFrames = 8;

        protected uint requestFrame;

        public Playback(GPU.Render.IContext renderContext)
		{
            requestFrame = 0;
            State = State.Empty;
            RenderContext = renderContext;
        }

        ~Playback()
        {
            Debug.Assert(!IsOpen());
            if (IsOpen())
                Close();
        }
        protected GPU.Render.IContext RenderContext { get; private set; }

        public virtual List<Essence> SupportedEssence { get; }
        public State State { get; protected set; }
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
            Debug.Assert(State != State.Stopped && State != State.Empty && FrameTimer != null);
            FrameTimer.Elapsed -= OnFrameTimer;
            FrameTimer.Stop();
            FrameTimer.Dispose();
            FrameTimer = null;
        }

        public void Play()
        {
            Debug.Assert(State == State.Stopped || State == State.Paused);

            // Start frame timer
            if (FrameTimer == null)
            {
                var frameDuration = TimeSpan.FromSeconds(1.0 / Clip.Metadata.Framerate.ToDouble());
                FrameTimer = new Timer(frameDuration.TotalMilliseconds);
                FrameTimer.Elapsed += OnFrameTimer;
            }
            FrameTimer.Start();
            State = State.Playing;
        }

        private void OnFrameTimer(object sender, ElapsedEventArgs e)
        {
            var displayDelay = bufferDurationFrames * Clip.Metadata.Framerate.ToDouble();
            RequestFrame(requestFrame);
            var displayFrame = requestFrame;

            var displayTimer = new Timer(displayDelay);
            displayTimer.Elapsed += (s, e) => 
            {
                DisplayFrame(displayFrame);
                displayTimer.Dispose();
            };
            displayTimer.AutoReset = false;
            displayTimer.Start();

            requestFrame++;
        }

        public void Pause()
        {
            Debug.Assert(State == State.Playing);

            // Stop frame timer
            FrameTimer.Stop();

            State = State.Paused;
        }

        public abstract Error RequestFrame(uint frameNumber);

        public abstract Error DisplayFrame(uint frameNumber);

        public abstract void OnRenderFrame(double timeInterval);
    }
}

