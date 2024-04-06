using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using InfiniteTool.Formats;
using InfiniteTool.GameInterop;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task SavePersistence(Visual root)
        {
            var data = new ProgressionData(0x1337, PersistenceEntries.ToList());

            var result = await TopLevel.GetTopLevel(root).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Select where to save Progression Data", 
                DefaultExtension = "infprog", 
                FileTypeChoices = new []
                {
                    new FilePickerFileType("InfiniteTool Progression File"){ Patterns = new string[] { "*.infprog" } }
                }
            });

            if (result != null)
            {
                using var file = await result.OpenWriteAsync();
                data.Write(file);
            }
        }
    }
}
