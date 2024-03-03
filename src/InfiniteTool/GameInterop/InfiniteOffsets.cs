using InfiniteTool.GameInterop.EngineDataTypes;
using Superintendent.Core;
using Superintendent.Core.Remote;
using Superintendent.Generation;
using System;
using System.Text.Json.Serialization;

namespace InfiniteTool
{
    [GenerateClient]
    public partial class InfiniteOffsets
    {
        public static readonly InfiniteOffsets Unknown = new();

        [SpanPointer]
        public Ptr<GameThreadTableEntry> ThreadTable { get; set; }

        [JsonPropertyName("game_revert")]
        public FunVoid game_revert { get; set; }

        public FunVoid game_save_fast { get; set; }

        [ParamNames("playerIndex")]
        public Fun<int, nint> player_get { get; set; } // Integer -> PlayerOrUnit

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

        [ParamNames("zero", "resultAddress", "stringAddress")]
        public FunVoid<int, nint, nint> Persistence_TryCreateKeyFromString { get; set; }

        [ParamNames("zero", "keyPointer", "valuePointer")]
        public FunVoid<int, nint, nint> Persistence_SetByteKey { get; set; }


        [ParamNames("zero", "keyPointer", "valuePointer")]
        public FunVoid<int, nint, nint> Persistence_SetBoolKey { get; set; }

        [ParamNames("zero", "keyPointer", "valuePointer")]
        public FunVoid<int, nint, nint>  Persistence_SetLongKey { get; set; }

        public FunVoid<bool> Game_TimeSetPaused { get; set; }

        public FunVoid<bool> ai_enable { get; set; }

        [SpanPointer]
        public Ptr<RuntimePersistenceBlock> RuntimePersistence { get; set; }

        public Ptr<nint> ThreadLocalStaticInitializer { get; set; }







        public nint Persistence_BatchTryCreateKeysFromStrings { get; set; }

        public nint Persistence_BatchGetKeyTypes { get; set; }

        public nint Persistence_BatchGetBoolKeys { get; set; }

        public nint Persistence_BatchGetByteKeys { get; set; }

        public nint Persistence_BatchGetLongKeys { get; set; }

        public nint Persistence_BatchGetBoolKeysForParticipant { get; set; }

        public nint Persistence_BatchGetByteKeysForParticipant { get; set; }

        public nint Persistence_BatchGetLongKeysForParticipant { get; set; }

        public nint Persistence_BatchSetBoolKeys { get; set; }

        public nint Persistence_BatchSetByteKeys { get; set; }

        public nint Persistence_BatchSetLongKeys { get; set; }

        public nint Persistence_BatchSetBoolKeysForParticipant { get; set; }

        public nint Persistence_BatchSetByteKeysForParticipant { get; set; }

        public nint Persistence_BatchSetLongKeysForParticipant { get; set; }

        public nint Persistence_BatchRemoveBoolKeyOverrides { get; set; }

        public nint Persistence_BatchRemoveByteKeyOverrides { get; set; }

        public nint Persistence_BatchRemoveLongKeyOverrides { get; set; }

        public nint Persistence_BatchRemoveBoolKeyOverrideForParticipant { get; set; }

        public nint Persistence_BatchRemoveByteKeyOverrideForParticipant { get; set; }

        public nint Persistence_BatchRemoveLongKeyOverrideForParticipant { get; set; }

        public nint Player_SaveLoadoutToPersistentStorage { get; set; }

        public nint StartLevel { get; set; }

        public nint StartLevelAtSpawn { get; set; }

        public nint ResetLevelAtSpawn { get; set; }


        


        public nint players { get; set; } // -> ObjectList

        public nint player_get_first_valid { get; set; }// -> PlayerOrUnit

        public nint camera_set_mode { get; set; } // PlayerOrUnit, Integer32->Void

        public nint object_cannot_take_damage { get; set; }

        public nint object_can_take_damage { get; set; }

        public nint object_cannot_die { get; set; }

        public nint unit_get_player { get; set; } // PlayerOrUnit->Participant

        public nint Persistence_SetLongKeyForParticipant { get; set; } // PersistenceKey,Participant,Integer32->Boolean

        public nint Persistence_RemoveLongKeyOverride { get; set; }

        public nint Persistence_RemoveLongKeyOverrideForParticipant { get; set; }

        public nint Persistence_TrackProgress { get; set; }

        

        public nint Persistence_GetLongKeyForParticipant { get; set; }

        public nint Persistence_GetLongKey { get; set; }

        public nint Object_GetPosition { get; set; }

        public nint Engine_CreateObject { get; set; }

        

        
    }
}
