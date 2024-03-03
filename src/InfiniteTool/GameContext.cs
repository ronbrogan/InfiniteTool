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
using static InfiniteTool.GameInterop.GamePersistence;

namespace InfiniteTool
{
    [AddINotifyPropertyChangedInterface]
    public class GameContext
    {
        private readonly ILogger<GameContext> logger;

        public GameInstance Instance { get; private set; }
        public GamePersistence Persistence { get; private set; }

        public ObservableCollection<TagInfo> Weapons { get; }


        public ObservableCollection<ProgressionEntry> PersistenceEntries { get; } = new();

        public TagInfo SelectedWeapon { get; set; }

        public GameContext(GameInstance instance, GamePersistence progression, ILogger<GameContext> logger)
        {
            this.Instance = instance;
            this.Persistence = progression;
            this.logger = logger;

            var weapTags = Tags.LoadWeaponTags();
            this.Weapons = new ObservableCollection<TagInfo>(weapTags.Weapons);
            foreach(var w in weapTags.WeaponConfigs)
                this.Weapons.Add(w);


        }

        public void RefreshPersistence()
        {
            PersistenceEntries.Clear();
            foreach (var e in this.Persistence.GetAllProgress())
                PersistenceEntries.Add(e);
        }
    }
}
