using System;
using static Octopus.Player.Core.Playback.IPlayback;

namespace Octopus.Player.Core.Pro
{
    public interface ILicense
    {
        bool Valid { get; }
        bool Enabled { get; }
    }
}