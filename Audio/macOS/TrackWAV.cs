﻿using System;
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

        private AVAudioPlayer Player { get; set; }

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
        }

        public void Play(float speed = 1.0f)
        {
            Player.Rate = speed;
            Player.Play();
        }

        public void Play(double position, float speed = 1.0f)
        {
            if (position < Duration)
            {
                Position = position;
                Player.Rate = speed;
                Player.Play();
            }
        }

        public void Stop()
        {
            Player.Stop();
            Position = 0;
        }

        private void OnFinishedPlaying(object sender, AVStatusEventArgs e)
        {
            if (Playing)
                Stop();
        }
    }
}

