using Octopus.Player.GPU.Compute;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal class Queue : IQueue
    {
        public string Name { get; private set; }
        private nint NativeHandle { get; set; }
        private Context Context { get; set; }

        internal Queue(Context context, string name = null)
        {
            Context = context;
            Name = name;
            
            int result;
            NativeHandle = Context.Handle.CreateCommandQueueWithProperties(Context.NativeHandle, Context.NativeDevice, (QueueProperties)0, out result);
            Debug.CheckError(result);
        }

        public void Dispose()
        {
            Debug.CheckError(Context.Handle.ReleaseCommandQueue(NativeHandle));
            NativeHandle = 0;
        }
    }
}
