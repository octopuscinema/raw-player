﻿using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.IO
{
    public abstract class Metadata : IMetadata
    {
        public uint DurationFrames { get; protected set; }

        public Maths.Rational Framerate { get; protected set; }

        public Vector2i Dimensions { get; protected set; }
    }
}
