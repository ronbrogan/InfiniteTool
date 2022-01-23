namespace InfiniteTool
{
    public class InfiniteOffsets
    {
        public nint MainThreadEntry { get; set; } = 0x467010;

        public nint Checkpoint_TlsIndexOffset { get; set; } = 0x49DF8BC;

        public nint RevertFlagOffset { get; set; } = 0x44FFEC8;

        public nint CheckpointInfoOffset { get; set; } = 0x4CC1440; // 

        public nint PlayerDatum_TlsIndexOffset { get; set; } = 0x5194E80;
        public nint ParticipantDatum_TlsIndexOffset { get; set; } = 0x5198158;

        public nint PersistenceUnknown_TlsIndexOffset { get; set; } = 0x49DF468;
        public nint PersistenceData_TlsIndexOffset { get; set; } = 0x52252A0;

        public nint Persistence_KeysFromStrings_Batch { get; set; } = 0x1497860;
        public nint Persistence_GetKeyTypes_Batch { get; set; } = 0x1497A90;

        public nint Persistence_GetBools_Batch { get; set; } = 0x1476E90;
        public nint Persistence_GetBytes_Batch { get; set; } = 0x1498A70;
        public nint Persistence_GetLongs_Batch { get; set; } = 0x1498800;
        public nint Persistence_GetBoolsForParticipant_Batch { get; set; } = 0x14771E0;
        public nint Persistence_GetBytesForParticipant_Batch { get; set; } = 0x1477630;
        public nint Persistence_GetLongsForParticipant_Batch { get; set; } = 0x1477810;

        public nint Persistence_SetBoolsForParticipant_Batch { get; set; } = 0x1475160;
        public nint Persistence_SetBytesForParticipant_Batch { get; set; } = 0x1475E00;
        public nint Persistence_SetLongsForParticipant_Batch { get; set; } = 0x1476AA0;

        public nint StartMap { get; set; } = 0x0;
        public nint StartMapAtSpawn { get; set; } = 0x0;
        public nint RestartMapAtSpawn { get; set; } = 0x0;
    }
}
