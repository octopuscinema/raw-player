﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Octopus.Player.Core.Maths
{
    public struct TimeCode
    {
        public ushort Frame { get; private set; }
        public ushort Second { get; private set; }
        public uint Minute { get; private set; }

        public ushort? Hour { get; private set; }

        public bool DropFrame { get; private set; }

        public TimeCode(ulong frames, uint framerate, bool dropFrame = false, bool useHours = false)
            : this()
        {
            Debug.Assert(framerate != 0);
            if (framerate != 0)
            {
                ulong TotalSeconds = frames / framerate;
                Frame = (ushort)(frames - TotalSeconds * framerate);
                Minute = (uint)(TotalSeconds / 60);
                Second = (ushort)(TotalSeconds - (ulong)Minute * 60);
            }
            if (useHours)
            {
                Hour = (ushort)((int)Minute / 60);
                Minute %= 60;
            }
            DropFrame = dropFrame;
        }

        public TimeCode(in IO.SMPTETimeCode timeCode)
            : this()
        {
            Frame = (ushort)((timeCode.FrameTens * 10) + timeCode.FrameUnits);
            Second = (ushort)((timeCode.SecondTens * 10) + timeCode.SecondUnits);
            Minute = (ushort)((timeCode.MinuteTens * 10) + timeCode.MinuteUnits);
            Hour = (ushort)((timeCode.HourTens * 10) + timeCode.HourUnits);
            DropFrame = timeCode.DropFlag;
        }

        public TimeCode(ulong frames, in Rational framerate, bool? dropFrame = null, bool useHours = false)
            : this()
        {
            Debug.Assert(!framerate.IsInfinity && !framerate.IsZero);
            if (framerate.IsInfinity || framerate.IsZero)
                return;

            if (!dropFrame.HasValue)
                dropFrame = (framerate == new Rational(30000, 1001) || framerate == new Rational(60000, 1001));
            DropFrame = dropFrame.Value;

            // Adapted from http://andrewduncan.net/timecodes/ and https://video.stackexchange.com/questions/22722/how-are-frames-in-59-94-drop-frame-timecode-dropped
            if (dropFrame.Value)
            {
                long dropFrames;
                long framesPer10Minutes;

                // Drop frame for 29.97
                if (framerate == new Rational(30000, 1001))
                {
                    dropFrames = 2;
                    framesPer10Minutes = 17982;
                }
                // Drop Frame for 59.94
                else if (framerate == new Rational(60000, 1001))
                {
                    dropFrames = 4;
                    framesPer10Minutes = 35964;
                }
                // Not a supported drop frame fps
                else
                {
                    Debug.Assert(false, "Attempting to use drop frame with a frame rate which doesn't support drop frame");
                    return;
                }

                var d = (long)frames / framesPer10Minutes;
                var m = (long)frames % framesPer10Minutes;

                frames += (ulong)(9 * dropFrames * d + dropFrames * ((m - dropFrames) / (framesPer10Minutes / 10)));

                var framerateInteger = (long)Math.Round(framerate.ToDouble());
                Frame = (ushort)((long)frames % framerateInteger);
                Second = (ushort)((long)frames / framerateInteger % 60);
                if (useHours)
                {
                    Minute = (uint)((long)frames / (framerateInteger * 60) % 60);
                    Hour = (ushort)((long)frames / (framerateInteger * 3600) % 24);
                } else
                    Minute = (uint)((long)frames / (framerateInteger * 60));
            }

            // Round up frame rate to whole number for time code calculations
            else
            {
                var framerateInteger = (ulong)Math.Round(framerate.ToDouble());
                ulong totalSeconds = frames / framerateInteger;
                Frame = (ushort)(frames - totalSeconds * (ulong)framerateInteger);
                Minute = (uint)(totalSeconds / 60);
                Second = (ushort)(totalSeconds - (ulong)Minute * 60);
                if (useHours)
                {
                    Hour = (ushort)((int)Minute / 60);
                    Minute %= 60;
                }
            }
        }

        public void Reset()
        {
            Frame = 0;
            Second = 0;
            Minute = 0;
            Hour = null;
        }

        public override string ToString()
        {
            string text = string.Empty;
            if ( Hour.HasValue )
                text += Hour.Value.ToString("D2") + ":";
            text += Minute.ToString("D2") + ":" + Second.ToString("D2");
            text += DropFrame ? ";" : ":";
            text += Frame.ToString("D2");
            return text;
        }

        public ulong TotalFrames(in Rational framerate)
        {
            Debug.Assert(!framerate.IsInfinity && !framerate.IsZero);
            if (framerate.IsInfinity || framerate.IsZero)
                return 0;

            var hours = Hour.HasValue ? Hour.Value : Minute / 60;
            var totalMinutes = Hour.HasValue ? (60*Hour.Value) + Minute : Minute;
            var minutes = Minute %= 60;
            
            // Adapted from: http://andrewduncan.net/timecodes/
            if (DropFrame)
            {
                // Drop frame for 29.97
                if (framerate == new Rational(30000, 1001))
                {
                    var frameNumber = 108000 * hours + 1800 * minutes
                        + 30 * Second + Frame
                        - 2 * (totalMinutes - (totalMinutes / 10));
                    return (ulong)frameNumber;
                }

                // Drop frame for 59.94
                else if (framerate == new Rational(60000, 1001))
                {
                    var frameNumber = 216000 * hours + 3600 * minutes
                        + 60 * Second + Frame
                        - 4 * (totalMinutes - (totalMinutes / 10));
                    return (ulong)frameNumber;
                }

                Debug.Assert(false, "Attempting to use drop frame with a frame rate which doesn't support drop frame");
            }

            var framerateInteger = (ulong)Math.Round(framerate.ToDouble());
            return Frame + (Second * framerateInteger) + ((ulong)totalMinutes * 60 * framerateInteger);
        }
    }
}
