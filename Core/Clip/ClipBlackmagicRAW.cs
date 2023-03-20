using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core
{
	public class ClipBlackmagicRAW : Clip
	{
        public override Essence Essence { get { return Essence.Video; } }

        public override IClip NextClip
        {
            get
            {
                var clips = EnumerateAdditionalClips();
                if (clips != null)
                {
                    for (int i = 0; i < clips.Count; i++)
                    {
                        if (clips[i].Path == Path && (i+1) < clips.Count)
                            return clips[i+1];
                    }
                }
                return null;
            }
        }
        public override IClip PreviousClip
        {
            get
            {
                var clips = EnumerateAdditionalClips();
                if (clips != null)
                {
                    for (int i = 0; i < clips.Count; i++)
                    {
                        if (clips[i].Path == Path && (i - 1) >= 0)
                            return clips[i - 1];
                    }
                }
                return null;
            }
        }

        public ClipBlackmagicRAW(string brawPath)
		{
            Path = brawPath;
            Valid = false;
            RawParameters = new RawParameters();
        }

        public override Error ReadMetadata(uint? frame = null)
        {
            return Error.NotImplmeneted;
        }

        private List<IClip> EnumerateAdditionalClips()
        {
            return new List<IClip>(){ };
        }

        public override Error Validate()
        {
            return Error.NotImplmeneted;
        }
    }
}

