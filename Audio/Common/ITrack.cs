﻿using System;

namespace Octopus.Player.Audio
{
    public interface ITrack : IDisposable
    {
        string Name { get; }
        double Duration { get; }
        double Position { get; set; }
        float Volume { get; set; }

        bool Playing { get; }

        void Play(float speed = 1.0f);
        void Play(double position, float speed = 1.0f);
        void Pause();
        void Stop();
    }
}