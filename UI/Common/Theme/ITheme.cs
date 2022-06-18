using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.UI
{
    public interface ITheme
    {
        Vector3 ClipBackground { get; }
        Vector3 EmptyBackground { get; }
    }
}
