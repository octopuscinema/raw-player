using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Audio
{
    public interface IMediaClock
    {
        bool Active { get; }
        double Time { get; }

        event EventHandler ActiveChanged;
    }
}
