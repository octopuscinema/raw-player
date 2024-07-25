using System;
using System.Collections.Generic;

namespace Octopus.Player.Audio
{
    public interface IContext
    {
        HashSet<ITrack> FetchTracks(IContainer container);
    }
}