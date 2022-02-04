using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static InfiniteTool.GameInterop.GamePersistence;

namespace InfiniteTool.Formats
{
    public class ProgressionData
    {
        private const string V1 = "InfiniteProgressV1";
        public const string CurrentVersion = V1;

        public uint Participant { get; }
        public List<ProgressionEntry> Entries { get; }

        public ProgressionData(uint participant, List<ProgressionEntry> entries)
        {
            this.Participant = participant;
            this.Entries = entries;
        }

        public void Write(Stream stream)
        {
            using var writer = new StreamWriter(stream);
            writer.WriteLine("InfiniteProgressV1");
            writer.WriteLine($"ParticipantID:0x{this.Participant:X}");
            writer.WriteLine("KeyName,DataType,GlobalValue,ParticipantValue");
            foreach (var entry in this.Entries)
            {
                writer.WriteLine($"{entry.KeyName},{entry.DataType},0x{entry.GlobalValue:X},0x{entry.ParticipantValue:X}");
            }
        }

        public static ProgressionData? FromStream(Stream stream)
        {
            using var reader = new StreamReader(stream);

            var version = reader.ReadLine();
            if (version != CurrentVersion) return null;

            var participant = reader.ReadLine();
            if(participant == null) return null;
            var participantId = Convert.ToUInt32(participant.Split(':')[1], 16);

            var header = reader.ReadLine();

            var entries = new List<ProgressionEntry>();

            string? entryLine = null;
            while ((entryLine = reader.ReadLine()) != null)
            {
                var parts = entryLine.Split(',');

                entries.Add(new ProgressionEntry()
                {
                    KeyName = parts[0],
                    DataType = parts[1],
                    GlobalValue = Convert.ToUInt32(parts[2], 16),
                    ParticipantValue = Convert.ToUInt32(parts[3], 16),
                });
            }

            return new ProgressionData(participantId, entries);
        }
    }
}
