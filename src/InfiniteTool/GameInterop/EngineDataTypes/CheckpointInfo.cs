using System.Runtime.InteropServices;

namespace InfiniteTool.GameInterop.EngineDataTypes
{
    [StructLayout(LayoutKind.Explicit)]
    public struct CheckpointInfo
    {
        [FieldOffset(0xC)]
        public byte CurrentSlot;

        [FieldOffset(0x10)]
        public uint LastSaveTicks;

        [FieldOffset(0x14)]
        public byte DangerousRevertCount;

        [FieldOffset(0x18)]
        public uint LastRevertTicks;

        [FieldOffset(0x20)]
        public nint Slot0;

        [FieldOffset(0x28)]
        public nint Slot1;

        [FieldOffset(0x30)]
        public uint Hash0;

        [FieldOffset(0x34)]
        public uint Hash1;

        [FieldOffset(0x50)]
        public nint SlotX;

        [FieldOffset(0x59)]
        public byte SuppressCheckpoints;
    }
}
