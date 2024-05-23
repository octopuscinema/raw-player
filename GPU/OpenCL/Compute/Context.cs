using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Octopus.Player.GPU.Compute;
using OpenTK.Mathematics;
using Silk.NET.OpenCL;

namespace Silk.NET.OpenCL.Extensions.APPLE
{
    internal class GCL
    {
        internal const string Library = "/System/Library/Frameworks/OpenCL.framework/OpenCL";
        internal const nint CL_CONTEXT_PROPERTY_USE_CGL_SHAREGROUP_APPLE = 0x10000000;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [DllImport(Library, EntryPoint = "gcl_gl_create_image_from_texture", ExactSpelling = true)]
        internal extern static IntPtr CreateImageFromTexture(IntPtr target, Int32 mip_level, UInt32 texture);

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [DllImport(Library, EntryPoint = "gcl_gl_set_sharegroup", ExactSpelling = true)]
        internal extern static void SetSharegroup(IntPtr share);
    }
}

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal static class Debug
    {
        static internal void CheckError(int returnValue)
        {
#if DEBUG
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if ((ErrorCodes)returnValue != ErrorCodes.Success)
                    throw new Exception("OpenCL Error: " + ((ErrorCodes)returnValue).ToString());
            }
            else
                System.Diagnostics.Debug.Assert((ErrorCodes)returnValue == ErrorCodes.Success, "OpenCL Error: " + ((ErrorCodes)returnValue).ToString());
#endif
        }
    }

	public class Context : IContext
    {
        public Api Api { get { return Api.OpenCL; } }

        public string ApiVersion { get; private set; }

        public string ApiName{ get; private set; }

        public string ApiVendor { get; private set; }

        public IQueue DefaultQueue { get { return defaultQueue; } }

        internal CL Handle { get; private set; }
        internal nint NativeHandle { get; set; }
        internal nint NativeDevice { get; set; }

        private bool SupportsGLSharing { get; set; }
        private bool SupportsAutoGLSync { get; set; }

        private IList<Program> programs;
        private IQueue defaultQueue;

        private Context(CL handle, DeviceType deviceType, bool supportsGLSharing, nint[] contextProperties)
        {
            Handle = handle;
            SupportsGLSharing = supportsGLSharing;
            unsafe
            {
                fixed (nint* properties = contextProperties)
                {
                    NativeHandle = Handle.CreateContextFromType(properties, deviceType, CallbackHandler, null, out int returnValue);
                    Debug.CheckError(returnValue);
                }
            }

            if (NativeHandle != 0)
                OnCreateContext();

            programs = new List<Program>();
        }

        private Context(CL handle, nint device, bool supportsGLSharing, nint[] contextProperties)
        {
            Handle = handle;
            SupportsGLSharing = supportsGLSharing;

            int returnValue;
            unsafe
            {
                fixed (nint* properties = contextProperties)
                {
                    NativeHandle = Handle.CreateContext(properties, 1, in device, CallbackHandler, null, out returnValue);
                    Debug.CheckError(returnValue);
                }
            }

            if (NativeHandle != 0)
                OnCreateContext();
        }

        private void OnCreateContext()
        {
            unsafe
            {
                nuint paramterSize;
                Debug.CheckError(Handle.GetContextInfo(NativeHandle, ContextInfo.Devices, 0, null, out paramterSize));

                nint[] devices = new nint[paramterSize / (nuint)sizeof(nint)];
                if (devices.Length == 0)
                    throw new Exception("Could not query OpenCL context device(s)");
                
                fixed (nint* p = devices)
                {
                    Debug.CheckError(Handle.GetContextInfo(NativeHandle, ContextInfo.Devices, paramterSize, p, null));
                }
                NativeDevice = devices[0];
            }

            SupportsAutoGLSync = DeviceExtensionSupported(Handle, NativeDevice, "cl_khr_gl_event");
            ApiName = GetDeviceInfo(Handle, NativeDevice, DeviceInfo.Name);
            ApiVendor = GetDeviceInfo(Handle, NativeDevice, DeviceInfo.Vendor);
            ApiVersion = GetDeviceInfo(Handle, NativeDevice, DeviceInfo.Version);
            Trace.WriteLine("Created OpenCL context for device: " + ApiName);

            defaultQueue = CreateQueue("defaultQueue");
        }

        private unsafe void CallbackHandler(byte* errinfo, void* privateInfo, nuint cb, void* userData)
        {

        }

        static private string GetDeviceInfo(CL handle, nint device, DeviceInfo deviceInfo)
        {
            nuint parameterSize;
            unsafe
            {
                handle.GetDeviceInfo(device, deviceInfo, 0, null, out parameterSize);
            }

            byte[] parameter = new byte[parameterSize];
            unsafe
            {
                fixed (byte* p = parameter)
                {
                    handle.GetDeviceInfo(device, deviceInfo, parameterSize, p, null);
                }
            }
            if (parameter.Length > 0 && parameter.Last() == 0)
                parameter = parameter.Take((int)parameterSize - 1).ToArray();
            return System.Text.Encoding.ASCII.GetString(parameter);
        }

        static private string GetPlatformInfo(CL handle, nint platform, PlatformInfo platformInfo)
        {
            nuint parameterSize;
            unsafe
            {
                handle.GetPlatformInfo(platform, platformInfo, 0, null, out parameterSize);
            }

            byte[] parameter = new byte[parameterSize];
            unsafe
            {
                fixed (byte* p = parameter)
                {
                    handle.GetPlatformInfo(platform, platformInfo, parameterSize, p, null);
                }
            }
            if (parameter.Length > 0 && parameter.Last() == 0)
                parameter = parameter.Take((int)parameterSize - 1).ToArray();
            return System.Text.Encoding.ASCII.GetString(parameter);
        }

        static private bool PlatformExtensionSupported(CL handle, nint platform, string extension)
        {
            var extensions = GetPlatformInfo(handle, platform, PlatformInfo.Extensions);
            return extensions.Contains(extension);
        }

        static private bool DeviceExtensionSupported(CL handle, nint device, string extension)
        {
            var extensions = GetDeviceInfo(handle, device, DeviceInfo.Extensions);
            return extensions.Contains(extension);
        }

        static nint[] GetGPUDevices(CL handle, nint platform)
        {
            unsafe
            {
                uint numDevices;
                handle.GetDeviceIDs(platform, DeviceType.Gpu, 0, null, out numDevices);

                var devices = new nint[numDevices];
                fixed (nint* p = devices)
                {
                    handle.GetDeviceIDs(platform, DeviceType.Gpu, numDevices, p, null);
                }
                return devices;
            }
        }

        static public Context CreateContext(GPU.Render.IContext renderContext)
        {
            var handle = CL.GetApi();

            uint numPlatforms;
            nint[] platforms;
            unsafe
            {
                handle.GetPlatformIDs(0, null, out numPlatforms);

                platforms = new nint[numPlatforms];
                fixed (nint* p = platforms)
                {
                    handle.GetPlatformIDs(numPlatforms, p, (uint*)null);
                }
                Trace.WriteLine("Discovered " + numPlatforms + " OpenCL platform(s)");
            }

            var platformsSupportingGLSharing = new List<nint>();
            foreach (var platform in platforms)
            {
                // Query platform devices
                var devices = GetGPUDevices(handle, platform);

                // Print platform info
                var name = GetPlatformInfo(handle, platform, PlatformInfo.Name);
                var vendor = GetPlatformInfo(handle, platform, PlatformInfo.Vendor);
                var version = GetPlatformInfo(handle, platform, PlatformInfo.Version);
                if ( PlatformExtensionSupported(handle, platform, "cl_khr_gl_sharing") || PlatformExtensionSupported(handle, platform, "cl_APPLE_gl_sharing") )
                {
                    platformsSupportingGLSharing.Add(platform);
                    Trace.WriteLine("CL Platform: " + name + ", Vendor: " + vendor + ", Version: " + version + ", GL/CL sharing support, " + devices.Length + " device(s)");
                } else
                    Trace.WriteLine("CL Platform: " + name + ", Vendor: " + vendor + ", Version: " + version + ", no CL/GL sharing support, " + devices.Length + " device(s)");

                // Print device(s) info
                foreach(var device in devices)
                {
                    var deviceName = GetDeviceInfo(handle, device, DeviceInfo.Name);
                    var deviceVersion = GetDeviceInfo(handle, device, DeviceInfo.Version);
                    string deviceContextSharing = (DeviceExtensionSupported(handle, device, "cl_khr_gl_sharing") || DeviceExtensionSupported(handle, device, "cl_APPLE_gl_sharing"))
                        ? "GL/CL sharing support" : "no CL/GL sharing support";
                    Trace.WriteLine("CL Device: " + deviceName + ", Version: " + deviceVersion + ", " + deviceContextSharing);
                }
            }

            // Pick the first platform with GL sharing, otherwise pick the first platform
            var bestPlatform = platformsSupportingGLSharing.Count > 0 ? platformsSupportingGLSharing.First() : platforms.First();
            if (platformsSupportingGLSharing.Count == 0)
                Trace.WriteLine("Warning, no OpenCL platforms with GL/CL sharing support");
            var supportsGLSharing = platformsSupportingGLSharing.Count > 0;

            // Get devices for this platform
            var bestPlatformDevices = GetGPUDevices(handle, bestPlatform);

            // Default context properties
            var defaultContextProperties = new nint[] {
                (nint)ContextProperties.Platform, bestPlatform
            };

            // Get shared GL device
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var GLSharingContextProperties = new nint[] {
                     Silk.NET.OpenCL.Extensions.APPLE.GCL.CL_CONTEXT_PROPERTY_USE_CGL_SHAREGROUP_APPLE,
                    renderContext.NativeDeviceContext, 0
                };
                Silk.NET.OpenCL.Extensions.APPLE.GCL.SetSharegroup(renderContext.NativeDeviceContext);

                return new Context(handle, DeviceType.Gpu, supportsGLSharing, supportsGLSharing ? GLSharingContextProperties : defaultContextProperties);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var GLSharingContextProperties = new nint[] {
                    (nint)Silk.NET.OpenCL.Extensions.KHR.KHR.GLContextKhr, renderContext.NativeContext,
                    (nint)Silk.NET.OpenCL.Extensions.KHR.KHR.WglHdcKhr, renderContext.NativeDeviceContext,
                    (nint)ContextProperties.Platform, bestPlatform
                };

                // Create context
                if (bestPlatformDevices.Length == 1 || !supportsGLSharing)
                    return new Context(handle, DeviceType.Gpu, supportsGLSharing, supportsGLSharing ? GLSharingContextProperties : defaultContextProperties);
                else
                {
                    // Get device currently associated with OpenGL Context
                    var sharingExtension = new Silk.NET.OpenCL.Extensions.KHR.KhrGlSharing(handle.Context);
                    unsafe
                    {
                        nint interopDevice;
                        nuint interopDeviceCount;
                        fixed (nint* properties = GLSharingContextProperties)
                        {
                            sharingExtension.GetGlcontextInfo(properties, Silk.NET.OpenCL.Extensions.KHR.GlContextInfo.CurrentDeviceForGLContext, 
                                (nuint)sizeof(nint), out interopDevice, out interopDeviceCount);
                        }

                        if (interopDevice == 0)
                        {
                            Trace.WriteLine("Could not find CL devices for the current GL context, compute not supported");
                            return null;
                        }

                        return new Context(handle, interopDevice, supportsGLSharing, GLSharingContextProperties);
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            if ( defaultQueue != null)
            {
                defaultQueue.Dispose();
                defaultQueue = null;
            }

            if (NativeHandle != 0)
            {
                Debug.CheckError(Handle.ReleaseContext(NativeHandle));
                NativeHandle = 0;
            }
        }

        public IProgram CreateProgram(Assembly assembly, string resourceName, IReadOnlyList<string> functions, IReadOnlyCollection<string> defines = null, string name = null)
        {
            if (!Path.HasExtension(resourceName))
                resourceName += ".cl";

            var resources = assembly.GetManifestResourceNames();
            foreach (string resource in resources)
            {
                if (resource.Contains(resourceName))
                {
                    var program = new Program(this, assembly, resource, functions, defines, name);
                    programs.Add(program);
                    return program;
                }
            }

            throw new Exception("Error locating OpenCL program resource: " + resourceName);
        }

        public IImage2D CreateImage(Vector2i dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess, 
            MemoryLocation memoryLocation = MemoryLocation.Default, string name = null)
        {
            return new Image2D(this, dimensions, format, memoryDeviceAccess, memoryHostAccess, memoryLocation, name);
        }

        public IImage2D CreateImage(Render.IContext renderContext, Render.ITexture texture, MemoryDeviceAccess memoryDeviceAccess)
        {
            return new Image2D(this, renderContext, texture, memoryDeviceAccess);
        }

        public IQueue CreateQueue(string name = null)
        {
            return new Queue(this, name);
        }
    }
}