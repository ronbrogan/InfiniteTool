using System.Text.Json.Serialization;

namespace InfiniteTool
{
    public class InfiniteOffsets
    {
        public nint? ThreadTable { get; set; }

        public nint? MainThreadEntry { get; set; }

        public nint Checkpoint_TlsIndexOffset { get; set; }

        public nint RevertFlagOffset { get; set; }

        public nint CheckpointInfoOffset { get; set; }

        public nint PlayerDatum_TlsIndexOffset { get; set; }
        public nint ParticipantDatum_TlsIndexOffset { get; set; }

        public nint PersistenceUnknown_TlsIndexOffset { get; set; }
        public nint PersistenceUnknown2_TlsIndexOffset { get; set; }

        public nint PersistenceData_TlsIndexOffset { get; set; }

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
    }
}
