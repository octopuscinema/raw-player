using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Player.Core.Maths;
using Octopus.Player.GPU;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.IO
{
    public interface IFrameMetadata
    {
        CFAPattern? CFAPattern { get; }
        float? BaselineExposure { get; }
        Timecode? TimeCode { get; }
    }
}