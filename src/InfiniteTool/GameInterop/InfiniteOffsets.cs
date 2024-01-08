using Superintendent.Core.Remote;
using System;
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

        public nint player_get_first_valid { get; set; }// -> PlayerOrUnit

        public nint camera_set_mode { get; set; } // PlayerOrUnit, Integer32->Void

        public nint object_cannot_take_damage { get; set; }

        public nint object_can_take_damage { get; set; }

        public nint object_cannot_die { get; set; }

        public nint Unit_RefillAmmo { get; set; }

        public nint Unit_RefillGrenades { get; set; }

        public nint CheckpointInfoAddress { get; set; }

        public nint Object_SetObjectCannotTakeDamage { get; set; }

        public nint unit_get_player { get; set; } // PlayerOrUnit->Participant

        public nint Persistence_SetLongKeyForParticipant { get; set; } // PersistenceKey,Participant,Integer32->Boolean

        public nint Persistence_TryCreateKeyFromString { get; set; }

        public nint Persistence_RemoveLongKeyOverride { get; set; }

        public nint Persistence_RemoveLongKeyOverrideForParticipant { get; set; }

        public nint Persistence_TrackProgress { get; set; }

        public nint Persistence_SetLongKey { get; set; }

        public nint Persistence_GetLongKeyForParticipant { get; set; }

        public nint Persistence_GetLongKey { get; set; }

        public nint Object_GetPosition { get; set; }

        public nint Engine_CreateObject { get; set; }

        public nint[] RuntimePersistenceChain { get; set; }

        public nint ResolveRuntimePersistenceChain(IRemoteProcess proc)
        {
            if (RuntimePersistenceChain.Length == 0)
                throw new ArgumentException("Bad point chain");

            proc.Read<nint>(RuntimePersistenceChain[0], out var curr);

            for (var i = 1; i < RuntimePersistenceChain.Length; i++)
            {
                proc.ReadAt<nint>(curr + RuntimePersistenceChain[i], out curr);
            }

            return curr;
        }
    }
}
