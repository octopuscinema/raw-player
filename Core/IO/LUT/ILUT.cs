using System;
using Octopus.Player.GPU;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.IO.LUT
{
	public interface ILUT : IDisposable
	{
		string Title { get; }
		GPU.Compute.IImage ComputeImage { get; }
        Format Format { get; }
    }

	public interface ILUT3D : ILUT
	{
		Vector3i Dimensions { get; }
	}
}

