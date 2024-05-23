using Octopus.Player.GPU.Compute;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    public class Buffer
    {
        public static MemFlags MemFlag(MemoryHostAccess access) 
        {
            switch (access)
            {
                case MemoryHostAccess.ReadWrite:
                    return MemFlags.None;
                case MemoryHostAccess.WriteOnly:
                    return MemFlags.HostWriteOnly;
                case MemoryHostAccess.ReadOnly:
                    return MemFlags.HostReadOnly;
                case MemoryHostAccess.NoAccess:
                    return MemFlags.HostNoAccess;
                default:
                    throw new ArgumentException("Unknown MemoryHostAccess setting");
            }
        }

        public static MemFlags MemFlag(MemoryDeviceAccess access)
        {
            switch (access)
            {
                case MemoryDeviceAccess.ReadWrite:
                    return MemFlags.ReadWrite;
                case MemoryDeviceAccess.WriteOnly:
                    return MemFlags.WriteOnly;
                case MemoryDeviceAccess.ReadOnly:
                    return MemFlags.ReadOnly;
                default:
                    throw new ArgumentException("Unknown MemoryDeviceAccess setting");
            }
        }

        public static MemFlags MemFlag(MemoryLocation location)
        {
            switch (location)
            {
                case MemoryLocation.Default:
                    return MemFlags.None;
                case MemoryLocation.Shared:
                    return MemFlags.AllocHostPtr;
                case MemoryLocation.SharedHostAllocate:
                    return MemFlags.UseHostPtr;
                default:
                    throw new ArgumentException("Unknown MemoryLocation setting");
            }
        }
    }
}
