using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Compute
{
    public enum MemoryLocation
    {
        Default,
        Shared,
        SharedHostAllocate
    };

    public enum MemoryDeviceAccess
    {
        ReadOnly,
		WriteOnly,
		ReadWrite
    };

    public enum MemoryHostAccess
    {
        ReadWrite,
        ReadOnly,
        WriteOnly,
        NoAccess
    };

    public interface IBuffer : IDisposable
    {
        MemoryLocation MemoryLocation { get; }
        MemoryDeviceAccess MemoryDeviceAccess { get; }
        MemoryHostAccess MemoryHostAccess { get; }
        string Name { get; }
        bool Valid { get; }
    }
}
