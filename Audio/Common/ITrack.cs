using System;

namespace Octopus.Player.Audio
{
    public interface ITrackProperties
    {
        uint ChannelCount { get; }
        uint BitDepth { get; }
        uint SampleRate { get; }
        Format Format { get; }
        UInt64 DurationFrames { get; }
    }

    public interface ITrack : IDisposable, IMediaClock
    {
        string Name { get; }
        double Duration { get; }
        double Position { get; set; }
        float Volume { get; set; }
        State State { get; }
        bool Muted { get; set; }

        void Play(float speed = 1.0f);
        void Play(double position, float speed = 1.0f);
        void Pause();
        void Stop();
    }
}