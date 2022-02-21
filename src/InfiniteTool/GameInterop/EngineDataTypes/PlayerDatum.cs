using System.Numerics;
using System.Runtime.InteropServices;

namespace InfiniteTool.GameInterop.EngineDataTypes
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct PlayerDatum
    {
        [FieldOffset(0x0)]
        public fixed byte HeaderString[12];

        [FieldOffset(192)]
        public Vector3 CameraPosition;

        [FieldOffset(204)]
        public Vector3 CameraRotation;

        [FieldOffset(292)]
        public Vector3 Position;
    }
}
