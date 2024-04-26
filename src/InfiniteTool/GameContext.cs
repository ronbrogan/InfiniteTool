using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using InfiniteTool.Formats;
using InfiniteTool.GameInterop;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using System;
using System.Collections.ObjectModel;
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
            try
            {
                Context.Instance.ToggleSkull(this.Name, this.Id);
            }
            catch { }
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class ActionItem
    {
        private readonly Action action;

        public ActionItem(Hotkeys hotkeys, string label, string id, Action action, Func<bool>? toggleStatusFunc = null)
        {
            this.Label = label;
            this.Id = id;
            this.action = action;
            this.ToggleStatusFunc = toggleStatusFunc;

            (Menu, TagValue) = KeyBinds.SetupBinding(Label, Id, action, hotkeys);
        }

        public void Invoke()
        {
            try
            {
                action();
            }
            catch { }
        }

        public Brush Background { get; set; }

        public ContextMenu Menu { get; set; }

        public object? TagValue { get; set; }

        public string Label { get; }

        public string Id { get; }

        public Func<bool>? ToggleStatusFunc { get; }
    }

    [AddINotifyPropertyChangedInterface]
    public class GameContext
    {
        private readonly Hotkeys hotkeys;
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

        public ObservableCollection<ActionItem> Actions { get; set; }

        public GameContext(GameInstance instance, GamePersistence progression, Hotkeys hotkeys, ILogger<GameContext> logger)
        {
            this.Instance = instance;
            this.Persistence = progression;
            this.hotkeys = hotkeys;
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

            Actions = new()
            {
                Action("Checkpont", "bindable_cp", Instance.TriggerCheckpoint),
                Action("Revert", "bindable_revert", Instance.TriggerRevert),
                Action("Double Revert", "bindable_doubleRevert", Instance.DoubleRevert),
                
                Action("Skip Cutscene", "bindable_cutsceneSkip", Instance.ForceSkipCutscene),
                Action("Suppress CPs", "bindable_toggleCheckpointSuppression", Instance.ToggleCheckpointSuppression, Instance.CheckpointsSuppressed),
                Action("Toggle Invuln","bindable_invuln", Instance.ToggleInvuln, Instance.PlayerIsInvulnerable),
                
                
                Action("Stop Time", "bindable_pause", Instance.TogglePause, Instance.GameIsPaused),
                Action("Suspend AI","bindable_aiSuspend", Instance.ToggleAi, Instance.AiDisabled),
                Action("Nuke All AI","bindable_aiNuke", Instance.NukeAi),
                
                Action("Restock", "bindable_restock", Instance.RestockPlayer),
            };

            //<Button Grid.Row="0" Grid.Column="0" x:Name="bindable_cp" Content="Checkpoint" HorizontalAlignment="Stretch"   VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="cp_Click"/>
			//<Button Grid.Row="0" Grid.Column="1" x:Name="bindable_revert" Content="Revert"  HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="revert_Click"/>
			//<Button Grid.Row="0" Grid.Column="2" x:Name="bindable_doubleRevert" Content="Double Revert" IsEnabled="True" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="doubleRevert_Click"/>
			//<Button Grid.Row="1" Grid.Column="0" x:Name="bindable_toggleCheckpointSuppression" Content="Suppress CPs" IsEnabled="True" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="suppressCp_Click"/>
			//<Button Grid.Row="1" Grid.Column="1" x:Name="bindable_invuln" Content="Toggle Invuln" IsEnabled="True" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="invulnToggle_Click"/>
			//<Button Grid.Row="1" Grid.Column="2" x:Name="bindable_restock" Content="Restock" IsEnabled="True" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="restock_Click"/>
			//<Button Grid.Row="2" Grid.Column="1" x:Name="bindable_pause" Content="Stop Time" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="stopTime_Click"/>
			//<Button Grid.Row="2" Grid.Column="2" x:Name="bindable_aiSuspend" Content="Suspend AI" HorizontalAlignment="Stretch"  VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="suspendAi_Click"/>
			//<Button Grid.Row="2" Grid.Column="0" x:Name="bindable_aiNuke" Content="Nuke All AI" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="12" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Click="nukeAi_Click"/>
        }

        private ActionItem Action(string label, string id, Action action, Func<bool>? toggleStatusFunc = null) => new ActionItem(this.hotkeys, label, id, action, toggleStatusFunc);

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

        public async Task LoadPersistence(Visual root)
        {
            var results = await TopLevel.GetTopLevel(root).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Progression Data to load",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("InfiniteTool Progression File"){ Patterns = new string[] { "*.infprog" } }
                }
            });

            var result = results.FirstOrDefault();

            if (result != null)
            {
                using var file = await result.OpenReadAsync();
                var prog = ProgressionData.FromStream(file);

                this.Persistence.SetProgress(prog.Entries);
            }

        }
    }
}
