using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace InfiniteTool.GameInterop.EngineDataTypes
{
    [StructLayout(LayoutKind.Explicit, Size = 0x201c)]
    public struct RuntimePersistenceBlock
    {
        [FieldOffset(0)]
        public int Version;

        [FieldOffset(4)]
        public int Count;

        [FieldOffset(8)]
        public int Unknown;

        [FieldOffset(12)]
        public RuntimePersistenceEntries Entries;
    }

    [System.Runtime.CompilerServices.InlineArray(400)]
    public struct RuntimePersistenceEntries
    {
        public RuntimePersistenceEntry _element0;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct RuntimePersistenceEntry
    {
        [FieldOffset(0)]
        public uint Key;

        [FieldOffset(4)]
        public uint RawValue;

        public int Value => (int)RawValue;
        public float FloatValue => BitConverter.Int32BitsToSingle((int)RawValue);
        public byte ByteValue => (byte)(RawValue << 24);
    }
}
