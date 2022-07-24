using System.Runtime.InteropServices;

namespace Octopus.Player.Core.IO
{
    [StructLayout(LayoutKind.Explicit)]
    public struct SMPTETimeCode
    {
        [FieldOffset(0)]
        private readonly ulong data;

        // Byte 0
        [FieldOffset(0)]
        private readonly byte frameUnits;
        [FieldOffset(4)]
        private readonly byte frameTens;
        [FieldOffset(6)]
        private readonly byte dropFlag;
        [FieldOffset(7)]
        private readonly byte unused;
        public uint FrameUnits { get { return Mask(frameUnits, 4); } }
        public uint FrameTens { get { return Mask(frameTens, 2); } }
        public bool DropFlag { get { return Mask(dropFlag, 1)!=0; } }

        // Byte 1
        [FieldOffset(8)]
        private readonly byte secondUnits;
        [FieldOffset(12)]
        private readonly byte secondTens;
        [FieldOffset(15)]
        private readonly byte flag1;
        public uint SecondUnits { get { return Mask(secondUnits, 4); } }
        public uint SecondTens { get { return Mask(secondTens, 3); } }
        public bool Flag1 { get { return Mask(flag1, 1)!=0; } }

        // Byte 2
        [FieldOffset(16)]
        private readonly byte minuteUnits;
        [FieldOffset(20)]
        private readonly byte minuteTens;
        [FieldOffset(23)]
        private readonly byte flag2;
        public uint MinuteUnits { get { return Mask(minuteUnits, 4); } }
        public uint MinuteTens { get { return Mask(minuteTens, 3); } }
        public bool Flag2 { get { return Mask(flag2, 1)!=0; } }

        // Byte 3
        [FieldOffset(24)]
        private readonly byte hourUnits;
        [FieldOffset(28)]
        private readonly byte hourTens;
        [FieldOffset(30)]
        private readonly byte flag3;
        [FieldOffset(31)]
        private readonly byte flag4;
        public uint HourUnits { get { return Mask(hourUnits, 4); } }
        public uint HourTens { get { return Mask(hourTens, 2); } }
        public bool Flag3 { get { return Mask(flag3, 1)!=0; } }
        public bool Flag4 { get { return Mask(flag4, 1)!=0; } }

        // Bytes 4-7 (BG Fields can contain date/time)
        [FieldOffset(32)]
        private readonly byte BG1_2;
        [FieldOffset(40)]
        private readonly byte BG3_4;
        [FieldOffset(48)]
        private readonly byte BG5_6;
        [FieldOffset(56)]
        private readonly byte BG7_8;

        static private uint Mask(byte data, int bitDepth)
        {
            return (uint)((2 << bitDepth) - 1) & data;
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

