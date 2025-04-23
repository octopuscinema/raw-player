using System.Collections.Generic;

namespace Octopus.Player.Core
{
    public class License : ILicense
    {
        public bool Valid => false;
        public bool Enabled => false;

        public List<SupportedFormat> SupportedFormats
        {
            get
            {
                var formats = new List<SupportedFormat>();

                // CinemaDNG always supported
                formats.Add(new SupportedFormat
                {
                    Codec = Codec.Dng,
                    Container = Container.Dng,
                    Name = "CinemaDNG",
                    ClipFactory = (path) => new ClipCinemaDNG(path)
                });

                return formats;
            }
        }

        public void OnWindowLoad(IPlayerWindow window)
        {
            
        }
    }
}