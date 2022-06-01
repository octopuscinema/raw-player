using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.IO
{
    public abstract class Metadata : IMetadata
    {
        public uint DurationFrames { get; private set; }

        public Dimensions Dimensions { get; private set; }
    }
}
