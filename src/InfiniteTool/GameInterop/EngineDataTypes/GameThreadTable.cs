using System;
using System.Runtime.InteropServices;

namespace InfiniteTool.GameInterop.EngineDataTypes
{
    [Flags]
    public enum ThreadPurpose
    {
        MainThread = 1,
        Debug = 2,
        Network = 4,
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct GameThreadTableEntry
    {
        [FieldOffset(0)]
        public byte IsRunning;

        [FieldOffset(1)]
        public fixed byte Name[32];

        [FieldOffset(72)]
        public uint Unknown2;

        [FieldOffset(80)]
        public uint ThreadId;

        [FieldOffset(84)]
        public ThreadPurpose Purpose;

        [FieldOffset(88)]
        public int Unknown3;

        [FieldOffset(92)]
        public int Unknown4;
    }
}
