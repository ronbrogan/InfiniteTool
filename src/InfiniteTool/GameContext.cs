using InfiniteTool.GameInterop;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace InfiniteTool
{
    [AddINotifyPropertyChangedInterface]
    public class GameContext
    {
        private readonly ILogger<GameContext> logger;

        public GameInstance Instance { get; private set; }
        public GamePersistence Persistence { get; private set; }

        public InfiniteMap SelectedMap { get; set; }

        public Dictionary<string, InfiniteMap> Maps { get; } = new()
        {
            ["Overworld"] = InfiniteMap.Overworld,
            ["Warship Gbraakon"] = InfiniteMap.WarshipGbraakon,
            ["Foundation"] = InfiniteMap.Foundation,
            ["Conservatory"] = InfiniteMap.Conservatory,
            ["Spire"] = InfiniteMap.Spire,
            ["Nexus"] = InfiniteMap.Nexus,
            ["The Command Spire"] = InfiniteMap.TheCommandSpire,
            ["Repository"] = InfiniteMap.Repository,
            ["House Of Reckoning"] = InfiniteMap.HouseOfReckoning,
            ["Silent Auditorium"] = InfiniteMap.SilentAuditorium,
        };

        public List<GamePersistence.Entry> PersistenceEntries { get; private set; } = new();

        public ObservableCollection<CheckpointData> Checkpoints { get; private set; } = new()
        {
            new CheckpointData("Checkpoing save/load coming soon...", TimeSpan.FromDays(7), new byte[0], null)
        };

        public CheckpointData SelectedCheckpoint { get; set; }

        public GameContext(GameInstance instance, GamePersistence progression, ILogger<GameContext> logger)
        {
            this.Instance = instance;
            this.Persistence = progression;
            this.logger = logger;
        }

        internal void StartSelectedLevel()
        {
            this.Instance.StartMap(this.SelectedMap);
        }

        internal void RefreshPersistence()
        {
            this.PersistenceEntries = this.Persistence.GetAllProgress();
        }

        internal void InjectSelectedCheckpoint()
        {
            this.Instance.InjectCheckpoint(SelectedCheckpoint.Data);
        }

        internal void SaveCurrentCheckpoint()
        {
            var data = this.Instance.SaveCheckpoint();

            if(data != null)
            {
                this.AddCheckpoint(data);
            }
        }

        private int cp = 0;
        public unsafe void AddCheckpoint(byte[] checkpointData, string filename = "")
        {
            string levelName;

            fixed (byte* cpPtr = checkpointData)
            {
                levelName = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(cpPtr + 12));
            }

            Checkpoints.Add(new CheckpointData(levelName, TimeSpan.FromSeconds(cp++), checkpointData, filename));
        }
    }
}
