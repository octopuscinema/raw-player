using Octopus.Player.GPU.Compute;
using Silk.NET.OpenCL;
using System;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal abstract class Image : IImage
    {
        public MemoryLocation MemoryLocation { get; protected set; }
        public MemoryDeviceAccess MemoryDeviceAccess { get; protected set; }
        public MemoryHostAccess MemoryHostAccess { get; protected set; }
        public string Name { get; protected set; }

        public Format Format { get; protected set; }

        internal static ImageFormat ImageFormat(Format format)
        {
            ImageFormat imageFormat = new ImageFormat();
            switch (format)
            {
                case GPU.Format.RGBA8:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt8;
                    imageFormat.ImageChannelOrder = ChannelOrder.Rgba;
                    break;
                case GPU.Format.RGBX8:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt8;
                    imageFormat.ImageChannelOrder = ChannelOrder.Rgbx;
                    break;
                case GPU.Format.RGB8:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt8;
                    imageFormat.ImageChannelOrder = ChannelOrder.Rgb;
                    break;
                case GPU.Format.R8:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt8;
                    imageFormat.ImageChannelOrder = ChannelOrder.R;
                    break;
                case GPU.Format.RGBA16:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt16;
                    imageFormat.ImageChannelOrder = ChannelOrder.Rgba;
                    break;
                case GPU.Format.RGBX16:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt16;
                    imageFormat.ImageChannelOrder = ChannelOrder.Rgbx;
                    break;
                case GPU.Format.RGB16:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt16;
                    imageFormat.ImageChannelOrder = ChannelOrder.Rgb;
                    break;
                case GPU.Format.R16:
                    imageFormat.ImageChannelDataType = ChannelType.UnormInt16;
                    imageFormat.ImageChannelOrder = ChannelOrder.R;
                    break;
                default:
                    throw new ArgumentException("Unknown GPU format");
            }

            return imageFormat;
        }

        public abstract void Dispose();
    }
}
