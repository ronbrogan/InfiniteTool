using System.Text.Json.Serialization;

namespace InfiniteTool
{
    public class InfiniteOffsets
    {
        public static readonly InfiniteOffsets Unknown = new();

        public nint? ThreadTable { get; set; }



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


        [JsonPropertyName("game_revert")]
        public nint GameRevert { get; set; }

        [JsonPropertyName("game_save_fast")]
        public nint GameSaveFast { get; set; }


        public nint players { get; set; } // -> ObjectList
        
        public nint player_get { get; set; } // Integer -> PlayerOrUnit

        public nint camera_set_mode { get; set; } // PlayerOrUnit, Integer32->Void

        public nint object_cannot_take_damage { get; set; }

        public nint object_can_take_damage { get; set; }

        public nint object_cannot_die { get; set; }

        public nint Unit_RefillAmmo { get; set; }

        public nint Unit_RefillGrenades { get; set; }

        public nint CheckpointInfoAddress { get; set; }
    }
}
