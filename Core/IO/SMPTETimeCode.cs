using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.IO
{
    [StructLayout(LayoutKind.Explicit)]
    public struct SMPTETimeCode
    {
        [FieldOffset(0)]
        private readonly uint lowInteger;
        [FieldOffset(4)]
		private readonly uint highInteger;

		// Byte 0
		public uint FrameUnits { get { return ReadSection(0,4); } }
		public uint FrameTens { get { return ReadSection(4, 2); } }
		public bool DropFlag { get { return ReadSection(6, 1)!=0; } }

		// Byte 1
		public uint SecondUnits { get { return ReadSection(8, 4); } }
		public uint SecondTens { get { return ReadSection(12, 3); } }
		public bool Flag1 { get { return ReadSection(15, 1) != 0; } }

		// Byte 2
		public uint MinuteUnits { get { return ReadSection(16, 4); } }
		public uint MinuteTens { get { return ReadSection(20, 3); } }
		public bool Flag2 { get { return ReadSection(23, 1) != 0; } }

		// Byte 3
		public uint HourUnits { get { return ReadSection(24, 4); } }
		public uint HourTens { get { return ReadSection(28, 2); } }
		public bool Flag3 { get { return ReadSection(30, 1) != 0; } }
		public bool Flag4 { get { return ReadSection(31, 1) != 0; } }

		private uint ReadSection(uint offset, uint length)
        {
			var pos = (int)(offset < 32 ? offset: offset-32);
			var word = offset < 32 ? lowInteger : highInteger;

			uint mask = ((((uint)1) << (int)length) - 1) << pos;
			return (word & mask) >> pos;
		}

        public override string ToString()
        {
            var hours = HourTens * 10 + HourUnits;
            var minutes = MinuteTens * 10 + MinuteUnits;
            var seconds = SecondTens * 10 + SecondUnits;
            var frames = FrameTens * 10 + FrameUnits;

            return DropFlag ? hours.ToString("D2") + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2") + ";" + frames.ToString("D2") :
                hours.ToString("D2") + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2") + ":" + frames.ToString("D2");
        }

        /*
        // Byte0
		u8 FrameUnits : 4;
		u8 FrameTens : 2;
		u8 DropFlag : 1;
		u8 Unused : 1;
		u8 :0;

		// Byte 1
		u8 SecondUnits : 4;
		u8 SecondTens : 3;
		u8 Flag1 : 1;
		u8 :0;

		// Byte2
		u8 MinuteUnits : 4;
		u8 MinuteTens : 3;
		u8 Flag2 : 1;
		u8 :0;

		// Byte3
		u8 HourUnits : 4;
		u8 HourTens : 2;
		u8 Flag4 : 1;
		u8 Flag3 : 1;
		u8 : 0;

		// Bytes4-7 (BG Fields can contain date/time)
		u8 BG1 : 4;
		u8 BG2 : 4;
		u8 : 0;
		u8 BG3 : 4;
		u8 BG4 : 4;
		u8 : 0;
		u8 BG5 : 4;
		u8 BG6 : 4;
		u8 : 0;
		u8 BG7 : 4;
		u8 BG8 : 4;
         */
    }
}

