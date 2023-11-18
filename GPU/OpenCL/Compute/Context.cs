using System.Diagnostics;
using System.Linq;
using Octopus.Player.GPU.Compute;
using Silk.NET.OpenCL;

namespace Octopus.Player.GPU.OpenCL.Compute
{
	public class Context : GPU.Compute.IContext
    {
        public Api Api { get { return Api.OpenCL; } }

        private CL Handle { get; set; }

        public Context()
		{
            
            Handle = CL.GetApi();

            uint numPlatforms;
            nint[] platforms;
            unsafe
            {
                Handle.GetPlatformIDs(0, null, out numPlatforms);

                platforms = new nint[numPlatforms];
                fixed (nint* p = platforms)
                {
                    Handle.GetPlatformIDs(numPlatforms, p, (uint*)null);
                }
                Trace.WriteLine("Discovered " + numPlatforms + " OpenCL platform(s)");
            }


            foreach (var platform in platforms)
            {
                // Query platform devices
                uint numDevices;
                nint[] devices;
                unsafe
                {
                    Handle.GetDeviceIDs(platform, DeviceType.Gpu, 0, null, out numDevices);

                    devices = new nint[numDevices];
                    fixed(nint* p = devices)
                    {
                        Handle.GetDeviceIDs(platform, DeviceType.Gpu, numDevices, p, null);
                    }
                }

                // Print platform info
                var name = GetPlatformInfo(platform, PlatformInfo.Name);
                var vendor = GetPlatformInfo(platform, PlatformInfo.Vendor);
                var version = GetPlatformInfo(platform, PlatformInfo.Version);
                Trace.WriteLine("CL Platform: " + name + ", Vendor: " + vendor + ", Version: " + version + ", " + numDevices + " device(s)");

                // Print device(s) info
                foreach(var device in devices)
                {
                    var deviceName = GetDeviceInfo(device, DeviceInfo.Name);
                    var deviceVersion = GetDeviceInfo(device, DeviceInfo.Version);
                    Trace.WriteLine("CL Device: " + deviceName + ", Version: " + deviceVersion );
                }
            }
        }

        private string GetDeviceInfo(nint device, DeviceInfo deviceInfo)
        {
            nuint parameterSize;
            unsafe
            {
                Handle.GetDeviceInfo(device, deviceInfo, 0, null, out parameterSize);
            }

            byte[] parameter = new byte[parameterSize];
            unsafe
            {
                fixed (byte* p = parameter)
                {
                    Handle.GetDeviceInfo(device, deviceInfo, parameterSize, p, null);
                }
            }
            if (parameter.Length > 0 && parameter.Last() == 0)
                parameter = parameter.Take((int)parameterSize - 1).ToArray();
            return System.Text.Encoding.ASCII.GetString(parameter);
        }

        private string GetPlatformInfo(nint platform, PlatformInfo platformInfo)
        {
            nuint parameterSize;
            unsafe
            {
                Handle.GetPlatformInfo(platform, platformInfo, 0, null, out parameterSize);
            }

            byte[] parameter = new byte[parameterSize];
            unsafe
            {
                fixed (byte* p = parameter)
                {
                    Handle.GetPlatformInfo(platform, platformInfo, parameterSize, p, null);
                }
            }
            if ( parameter.Length >0 && parameter.Last() == 0 )
                parameter = parameter.Take((int)parameterSize - 1).ToArray();
            return System.Text.Encoding.ASCII.GetString(parameter);
        }
    }
}

