namespace InfiniteTool
{
    public class InfiniteOffsets
    {
        public nint MainThreadEntry => 0x467010;
        public nint Checkpoint_TlsIndexOffset => 0x49DF8BC;
        public nint RevertFlagOffset => 0x44FFEC8;


        public nint PlayerDatum_TlsIndexOffset => 0x5194E80;
        public nint ParticipantDatum_TlsIndexOffset => 0x5198158;

        public nint PersistenceUnknown_TlsIndexOffset => 0x49DF468;
        public nint PersistenceData_TlsIndexOffset => 0x52252A0;

        public nint Persistence_KeysFromStrings_Batch => 0x1497860;
        public nint Persistence_GetKeyTypes_Batch => 0x1497A90;

        public nint Persistence_GetBools_Batch => 0x1476E90;
        public nint Persistence_GetBytes_Batch => 0x1498A70;
        public nint Persistence_GetLongs_Batch => 0x1498800;
        public nint Persistence_GetBoolsForParticipant_Batch => 0x14771E0;
        public nint Persistence_GetBytesForParticipant_Batch => 0x1477630;
        public nint Persistence_GetLongsForParticipant_Batch => 0x1477810;

        public nint Persistence_SetBoolsForParticipant_Batch => 0x1475160;
        public nint Persistence_SetBytesForParticipant_Batch => 0x1475E00;
        public nint Persistence_SetLongsForParticipant_Batch => 0x1476AA0;

    }
}
