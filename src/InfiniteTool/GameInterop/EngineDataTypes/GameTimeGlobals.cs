using System.Runtime.InteropServices;

namespace InfiniteTool.GameInterop.EngineDataTypes
{
    [StructLayout(LayoutKind.Explicit)]
    public struct GameTimeGlobals
    {
        public const int TickRateReciprocalOffset = 120;
        public const int GameTimeMultiplierOffset = 132;


        [FieldOffset(TickRateReciprocalOffset)]
        public float TickRateReciprocal;

        [FieldOffset(GameTimeMultiplierOffset)]
        public float GameTimeMultiplier;

        [FieldOffset(142)]
        public short Suspended;
    }
}
