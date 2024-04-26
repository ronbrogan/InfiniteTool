using InfiniteTool.GameInterop.EngineDataTypes;
using Superintendent.Core;
using Superintendent.Core.Remote;
using Superintendent.Generation;
using System;
using System.Text.Json.Serialization;

namespace InfiniteTool
{
    [GenerateClient]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public partial class InfiniteOffsets
    {
        public static readonly InfiniteOffsets Unknown = new();

        [SpanPointer]
        public Ptr<GameThreadTableEntry> ThreadTable { get; set; }

        public FunVoid game_revert { get; set; }

        public FunVoid game_save_fast { get; set; }

        [ParamNames("playerIndex")]
        public Fun<int, nint> player_get { get; set; }

        [ParamNames("tagId", "targetObjectId")]
        public Fun<uint, nint, nint> Object_PlaceTagAtObjectLocation { get; set; }

        [ParamNames("unitId")]
        public FunVoid<nint> Unit_RefillAmmo { get; set; }

        [ParamNames("unitId", "grenadeType")]
        public FunVoid<nint, int> Unit_RefillGrenades { get; set; }

        [ParamNames("obj", "value")]
        public FunVoid<nint, float> object_set_shield { get; set; }

        public Ptr<CheckpointInfo> CheckpointInfo { get; set; }

        [ParamNames("objectId", "value")]
        public FunVoid<nint, bool> Object_SetObjectCannotTakeDamage { get; set; }

        [ParamNames("objectId")]
        public Fun<nint, bool> Object_GetObjectCannotTakeDamage { get; set; }

        [ParamNames("zero", "resultAddress", "stringAddress")]
        public FunVoid<int, nint, nint> Persistence_TryCreateKeyFromString { get; set; }

        [ParamNames("zero", "keyPointer", "valuePointer")]
        public FunVoid<int, nint, nint> Persistence_SetByteKey { get; set; }

        [ParamNames("zero", "keyPointer", "valuePointer")]
        public FunVoid<int, nint, nint> Persistence_SetBoolKey { get; set; }

        [ParamNames("zero", "keyPointer", "valuePointer")]
        public FunVoid<int, nint, nint>  Persistence_SetLongKey { get; set; }

        public FunVoid<bool> Game_TimeSetPaused { get; set; }

        public FunVoid<int> Game_SetTps { get; set; }

        public FunVoid<bool> ai_enable { get; set; }

        public FunVoid ai_kill_all { get; set; }


        [SpanPointer]
        public Ptr<RuntimePersistenceBlock> RuntimePersistence { get; set; }

        public Ptr<nint> ThreadLocalStaticInitializer { get; set; }


        [ParamNames("zero", "keysResult", "strings")]
        public FunVoid<int, nint, nint> Persistence_BatchTryCreateKeysFromStrings { get; set; }

        [ParamNames("zero", "values", "keys")]
        public FunVoid<int, nint, nint> Persistence_BatchGetBoolKeys { get; set; }

        [ParamNames("zero", "values", "keys")]
        public FunVoid<int, nint, nint> Persistence_BatchGetByteKeys { get; set; }

        [ParamNames("zero", "values", "keys")]
        public FunVoid<int, nint, nint> Persistence_BatchGetLongKeys { get; set; }

        [ParamNames("zero", "values", "keys", "results")]
        public FunVoid<int, nint, nint, nint> Persistence_BatchSetBoolKeys { get; set; }

        [ParamNames("zero", "values", "keys", "results")]
        public FunVoid<int, nint, nint, nint> Persistence_BatchSetByteKeys { get; set; }

        [ParamNames("zero", "values", "keys", "results")]
        public FunVoid<int, nint, nint, nint> Persistence_BatchSetLongKeys { get; set; }


        [ParamNames("playerId")]
        public Fun<int, nint> GetMessageBuffer { get; set; }

        [ParamNames("bufferSection", "playerId", "playerId2")]
        public Fun<nint, int, int, nint> GetMessageBufferSlot { get; set; }

        [ParamNames("buffer", "bufferSlot", "duration", "stringPointerUtf16")]
        public FunVoid<int, nint, float, nint> ShowMessage { get; set; }

        [ParamNames("skullId", "enable")]
        public FunVoid<int, bool> skull_enable { get; set; }

        [ParamNames("skullId")]
        public Fun<int, bool> is_skull_active { get; set; }

        public FunVoid composer_debug_cinematic_skip { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
