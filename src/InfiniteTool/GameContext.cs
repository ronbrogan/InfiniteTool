using InfiniteTool.GameInterop;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace InfiniteTool
{

    public class GameContext : INotifyPropertyChanged
    {
        private readonly ILogger<GameContext> logger;

        public event PropertyChangedEventHandler? PropertyChanged;

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
    }
}
