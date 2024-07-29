using System;
using System.Drawing.Imaging;
using System.Security.Policy;
using AVFoundation;
using Foundation;

namespace Octopus.Player.Audio.macOS
{
    public class TrackWAV : ITrack
    {
        public string Name { get; private set; }

        public double Duration { get { return Player.Duration; } }

        public double Position
        {
            get { return Player.CurrentTime; }
            set { Player.CurrentTime = value; }
        }

        public float Volume
        {
            get { return Player.Volume; }
            set { Player.Volume = value; }
        }

        public bool Playing { get { return Player.Playing; } }

        public bool Muted
        {
            get { return Volume == 0.0f && unmutedVolume.HasValue; }
            set
            {
                if ( value )
                {
                    unmutedVolume = Volume;
                    Volume = 0.0f;
                }
                else if ( unmutedVolume.HasValue )
                {
                    Volume = unmutedVolume.Value;
                    unmutedVolume = null;
                }
            }
        }

        private float? unmutedVolume;

        private AVAudioPlayer Player { get; set; }

        public event EventHandler ActiveChanged;

        public bool Active { get { return Playing; } }

        public double Time { get { return Position; } }

        public TrackWAV(string wavPath)
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(wavPath);
            Player = AVAudioPlayer.FromUrl(NSUrl.FromFilename(wavPath));
            Player.NumberOfLoops = 0;
            Player.FinishedPlaying += OnFinishedPlaying;
            Player.EnableRate = true;
            Player.PrepareToPlay();
        }

        public void Dispose()
        {
            Player.FinishedPlaying -= OnFinishedPlaying;
            if (Player.Playing)
                Player.Stop();
            Player.Dispose();
        }

        public void Pause()
        {
            Player.Pause();
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Play(float speed = 1.0f)
        {
            Player.Rate = speed;
            Player.Play();
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Play(double position, float speed = 1.0f)
        {
            if (position < Duration)
            {
                Position = position;
                Player.Rate = speed;
                Player.Play();
                ActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Stop()
        {
            Player.Stop();
            Position = 0;
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnFinishedPlaying(object sender, AVStatusEventArgs e)
        {
            if (Playing)
                Stop();
        }
    }
}

