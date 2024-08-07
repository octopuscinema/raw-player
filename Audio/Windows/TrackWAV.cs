﻿using System.Diagnostics;
using System.Windows.Media;

namespace Octopus.Player.Audio.Windows
{
    public class TrackWAV : ITrack
    {
        public string Name { get; private set; }

        public double Duration { get { return Player.NaturalDuration.TimeSpan.TotalSeconds; } }

        public double Position
        {
            get { return Player.Position.TotalSeconds; }
            set { Player.Position = TimeSpan.FromSeconds(value); }
        }

        public float Volume
        {
            get { return (float)Player.Volume; }
            set { Player.Volume = value; }
        }

        public bool Playing { get; private set; }

        private MediaPlayer Player { get; set; }
        public bool Muted 
        { 
            get { return Player.IsMuted; }
            set { Player.IsMuted = value; }
        }

        public event EventHandler ActiveChanged;

        public bool Active { get { return Playing; } }

        public double Time { get { return Position; } }

        public TrackWAV(string wavPath)
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(wavPath);
            Player = new MediaPlayer();
            Player.Open(new Uri( wavPath));
        }

        public void Dispose()
        {
            if (Playing)
                Player.Stop();
            Player.Close();
        }

        public void Pause()
        {
            Player.Pause();
            Playing = false;
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Play(float speed = 1.0f)
        {
            Player.SpeedRatio = speed;
            Player.Play();
            Playing = true;
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Play(double position, float speed = 1.0f)
        {
            if (position < Duration)
            {
                Position = position;
                Player.SpeedRatio = speed;
                Player.Play();
                Playing = true;
                ActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Stop()
        {
            Player.Stop();
            Playing = false;
            ActiveChanged?.Invoke(this, EventArgs.Empty);
            Position = 0;
        }
    }
}

