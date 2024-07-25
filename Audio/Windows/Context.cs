using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Octopus.Player.Audio.Windows
{
	public class Context : IContext
	{
		public Context()
		{
		}

        public HashSet<ITrack> FetchTracks(IContainer container)
        {
            switch (container.AudioCodec)
            {
                case Codec.Wav:
                    if (Directory.Exists(container.Path))
                    {
                        // Look for default wav name
                        var defaultWavName = Path.GetFileName(container.Path) + ".wav";
                        var defaultWavPath = Path.Combine(container.Path, defaultWavName);
                        if ( File.Exists(defaultWavPath) )
                            return new HashSet<ITrack>() { new TrackWAV(defaultWavPath) };

                        // If we got here, we'll need to locate the wavs
                        try
                        {
                            var wavFiles = Directory.EnumerateFiles(container.Path, "*.wav", SearchOption.TopDirectoryOnly);
                            if (wavFiles.Any())
                            {
                                HashSet<ITrack> tracks = new HashSet<ITrack>();
                                foreach (var wavFile in wavFiles)
                                    tracks.Add(new TrackWAV(wavFile));
                                return tracks;
                            }
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine("Could not enumerate files in: '" + container.Path + "'\n" + e.Message);
                            return null;
                        }
                    }
                    return null;
                default:
                    return null;
            }
        }
    }
}

