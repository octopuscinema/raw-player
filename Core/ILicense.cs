using System;
using System.Collections.Generic;

namespace Octopus.Player.Core
{
    public class SupportedFormat
    { 
        public Codec Codec { get; set; }
        public Container Container { get; set; }
        public string Name { get; set; }
        public Func<string, IClip> ClipFactory { get; set; }
    }

    public interface ILicense
    {
        bool Valid { get; }
        bool Enabled { get; }

        List<SupportedFormat> SupportedFormats { get; }

        void OnWindowLoad(IPlayerWindow window);
    }
}