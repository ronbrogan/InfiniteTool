using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
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
    public record SkullInfo(GameContext Context, string Name, int Id)
    {
        public void ToggleSkull()
        {
            Context.Instance.ToggleSkull(this.Name, this.Id);
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class GameContext
    {
        private readonly ILogger<GameContext> logger;

        public GameInstance Instance { get; private set; }
        public GamePersistence Persistence { get; private set; }

        public ObservableCollection<TagInfo> Weapons { get; }
        public ObservableCollection<TagInfo> Vehicles { get; }
        public ObservableCollection<TagInfo> Bipeds { get; }


        public ObservableCollection<ProgressionEntry> PersistenceEntries { get; } = new();
        public FlatTreeDataGridSource<ProgressionEntry> PersistenceEntriesSource { get; }

        public TagInfo SelectedWeapon { get; set; }
        public TagInfo SelectedVehicle { get; set; }
        public TagInfo SelectedBiped { get; set; }

        public ObservableCollection<SkullInfo> Skulls { get; set; }

        public GameContext(GameInstance instance, GamePersistence progression, ILogger<GameContext> logger)
        {
            this.Instance = instance;
            this.Persistence = progression;
            this.logger = logger;

            var tags = Tags.LoadTags();
            this.Weapons = new ObservableCollection<TagInfo>(tags.Weapons);
            this.Vehicles = new ObservableCollection<TagInfo>(tags.Vehicles);
            this.Bipeds = new ObservableCollection<TagInfo>(tags.Bipeds);

            PersistenceEntriesSource = new FlatTreeDataGridSource<ProgressionEntry>(this.PersistenceEntries)
            {
                Columns =
                {
                    new TextColumn<ProgressionEntry, string>("Type", e => e.DataType),
                    new TextColumn<ProgressionEntry, string>("Key Name", e => e.KeyName),
                    new TextColumn<ProgressionEntry, string>("Global Value", e => e.GlobalValueString),
                    new TextColumn<ProgressionEntry, string>("User Value", e => e.ParticipantValueString),
                }
            };

            Skulls = new()
            {
                new (this, "Black Eye", 0),
                new (this, "Catch", 1),
                new (this, "Fog", 2),
                new (this, "Famine", 3),
                new (this, "Thunderstorm", 4),
                new (this, "Mythic", 5),
                new (this, "Blind", 6),
                new (this, "Boom", 7),
                new (this, "Cowbell", 8),
                new (this, "Grunt Birthday Party", 9),
                new (this, "IWHBYD", 10),
                new (this, "Bandana", 11)
            };
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
