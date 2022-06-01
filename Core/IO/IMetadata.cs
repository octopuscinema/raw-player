using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.IO
{
    public struct Dimensions
    {
        int x;
        int y;
    }

    public interface IMetadata
    {
        uint DurationFrames { get; }

        Dimensions Dimensions { get; }
    }
}
