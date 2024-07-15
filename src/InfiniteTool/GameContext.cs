using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using InfiniteTool.Credentials;
using InfiniteTool.Extensions;
using InfiniteTool.Formats;
using InfiniteTool.GameInterop;
using InfiniteTool.GameInterop.Internal;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static InfiniteTool.GameInterop.GamePersistence;

namespace InfiniteTool
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class SkullActionItem : ActionItem
    {
        public SkullActionItem(GameContext context, string name, int id)
            : base(name, "skull" + StringExtensions.ToCamelCase(name, true), 
                  () => context.Instance.ToggleSkull(name, id),
                  () => context.Instance.GetSkullState(id))
        {
        }
    }

    [AddINotifyPropertyChangedInterface]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class ActionItem : BindableUiAction
    {
        private readonly Func<Task> action;
        private Func<bool>? toggleStatusFunc;

        private Brush defaultBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255));
        private Brush toggledOnBrush = new SolidColorBrush(Color.FromArgb(128, 2, 120, 255));

        public IReactiveCommand InvokeCommand { get; set; }

        public Expression<Func<GameContext, bool>>? Guard { get; set;}

        public ActionItem(string label, string id, Func<Task> action, Func<bool>? toggleStatusFunc = null)
        {
            this.Label = label;
            this.Id = id;
            this.action = action;
            this.toggleStatusFunc = toggleStatusFunc;
        }

        public ActionItem WithGuard(Expression<Func<GameContext, bool>> guard)
        {
            this.Guard = guard;
            return this;
        }

        public override async Task Invoke()
        {
            if (!this.IsEnabled()) return;

            await InvokeRaw();

            if (toggleStatusFunc != null)
                ToggleState = GetToggleState();
        }

        public async Task InvokeRaw()
        {
            try
            {
                await action();
            }
            catch { }
        }

        public bool GetToggleState()
        {
            if (this.toggleStatusFunc == null) return false;

            try
            {
                return this.toggleStatusFunc();

            } catch { return false; }
        }

        public Brush BackgroundBrush => ToggleState ? toggledOnBrush : defaultBrush;

        public bool ToggleState { get; set; }

        public bool IsToggleAction => toggleStatusFunc != null;

        public bool OnlyValidWhilePlaying { get; }
    }

    [AddINotifyPropertyChangedInterface]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class GameContext
    {
        private readonly ILogger<GameContext> logger;
        private ArenaAllocator allocator;

        public GameInstance Instance { get; private set; }
        public GamePersistence Persistence { get; private set; }

        public ObservableCollection<TagInfo> Weapons { get; }
        public ObservableCollection<TagInfo> Vehicles { get; }
        public ObservableCollection<TagInfo> Bipeds { get; }


        public ObservableCollection<ProgressionEntry> PersistenceEntries { get; } = new();
        public FlatTreeDataGridSource<ProgressionEntry> PersistenceEntriesSource { get; }

        public TagInfo? SelectedWeapon { get; set; }
        public TagInfo? SelectedVehicle { get; set; }
        public TagInfo? SelectedBiped { get; set; }

        public ObservableCollection<SkullActionItem> Skulls { get; set; }

        public ObservableCollection<ActionItem> Actions { get; set; }

        public ObservableCollection<ActionItem> Hacks { get; set; }

        public string PlayerVelocity { get; set; }

        public bool ProbablyInGame { get; set; }
        public bool Paused { get; set; }
        public bool InCutscene { get; set; }
        public bool HasProcess { get; set; }

        public bool MapResetOnLoad { get; set; } = true;

        public bool ShouldPersistToggles { get; set; }

        public bool ShouldEnforceEquipment { get; set; }

        public bool AdvancedMode { get; set; }

        public bool ReallyAdvancedMode { get; set; }

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

            Actions = new()
            {
                new("Checkpont", "cp", Instance.TriggerCheckpoint),
                new("Revert", "revert", Instance.TriggerRevert),
                new("Double Revert", "doubleRevert", Instance.DoubleRevert),

                new ActionItem("Skip Cutscene", "cutsceneSkip", Instance.ForceSkipCutscene).WithGuard(c => c.InCutscene),
                new("Suppress CPs", "toggleCheckpointSuppression", Instance.ToggleCheckpointSuppression, Instance.CheckpointsSuppressed),
                new("Show Coords", "pancam", Instance.ToggleCoords, Instance.CoordsOn),

                new("Reload Map", "mapReset", async () => {using (var l = await Instance.StartExclusiveOperation()) Instance.Engine.map_reset(); }),
            };

            Hacks = new()
            {
                new("Slow-mo", "slowMoGuys", Instance.ToggleSlowMo, Instance.SlowMoActivated),
                new("Fast-mo", "fastMoGals", Instance.ToggleFastMo, Instance.FastMoActivated),
                new("Stop Time", "pause", Instance.ToggleGameTimePause, Instance.GameTimeIsPaused),

                new("Suspend AI","aiSuspend", Instance.ToggleAi, Instance.AiDisabled),
                new("Nuke All AI","aiNuke", Instance.NukeAi),
                new("Toggle Invuln","invuln", Instance.ToggleInvuln, Instance.PlayerIsInvulnerable),

                new("No clip", "noclip", Instance.TogglePlayerNoClip, Instance.PlayerNoClip),
                new("Fly cam", "flycam", Instance.ToggleFlyCam, Instance.FlycamEnabled),
                new("Restock", "restock", Instance.RestockPlayer),
            };

            // Register all current action items with hotkeys, etc
            WireUpActions();

            instance.OnAttach += Instance_OnAttach;
            instance.BeforeDetach += Instance_BeforeDetach;
        }

        private void Instance_BeforeDetach(object? sender, EventArgs e)
        {
            periodicActionsCts.Cancel();
            Dispatcher.UIThread.Invoke(() =>
            {
                this.HasProcess = false;
            });
        }

        private void Instance_OnAttach(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                this.HasProcess = true;
                this.allocator = new ArenaAllocator(this.Instance.RemoteProcess, 4 * 1024 * 1024); // 4MB working area
                this.LoadToggleStates();
                _ = PeriodicLoop();
            });
        }

        private IEnumerable<ActionItem> AllActionItems => Actions.Concat(Skulls).Concat(Hacks);

        private void WireUpActions()
        {
            KeyBinds.SetupActions(AllActionItems);

            foreach(var action in AllActionItems)
            {
                if(action.Guard != null)
                {
                    action.IsEnabled = () => action.Guard.Compile().Invoke(this) || this.AdvancedMode;
                    action.InvokeCommand = ReactiveCommand.Create(action.Invoke, this.WhenAnyValue(action.Guard, c => c.AdvancedMode, (a, b) => a || b));
                }
                else
                {
                    // safeguard for hotkey invokes
                    action.IsEnabled = () => this.ProbablyInGame || this.AdvancedMode;
                    action.InvokeCommand = ReactiveCommand.Create(action.Invoke, this.WhenAnyValue(c => c.ProbablyInGame, c => c.AdvancedMode, (a, b) => a || b));
                }
            }
        }

        // on app startup, pull any toggle states from the game
        // defaults are off, but if the tool is restarted while a toggle is on, we want to
        // keep the current state as the desired state
        private void LoadToggleStates()
        {
            foreach(var item in AllActionItems.Where(i => i.IsToggleAction))
            {
                item.ToggleState = item.GetToggleState();
            }
        }

        // this is called while the tool is running to ensure that any toggles the
        // user has enabled are not reset by reverts, etc
        private nint lastRefreshTime = nint.MaxValue;
        private CancellationTokenSource periodicActionsCts = new();
        private DateTimeOffset lastToggleRefresh = DateTimeOffset.MinValue;

        private nint velocityLocation = nint.MaxValue;

        private async Task PeriodicLoop()
        {
            periodicActionsCts = new CancellationTokenSource();

            await Task.Delay(250);

            while (!periodicActionsCts.IsCancellationRequested)
            {
                await Task.Delay(100);

                using (var l = await Instance.StartExclusiveOperation())
                {
                    this.ProbablyInGame = Instance.ProbablyIsInGame();
                    this.Paused = Instance.IsPaused();
                    this.InCutscene = Instance.InCutscene();
                    var curTime = this.Instance.Engine.Engine_GetCurrentTime();

                    if (this.Instance.InMainMenu() || curTime == 0)
                    {
                        lastRefreshTime = nint.MaxValue;
                        continue;
                    }

                    var player = this.Instance.Engine.player_get(0);

                    if (ProbablyInGame && curTime < lastRefreshTime && this.ShouldEnforceEquipment)
                    {
                        await EnforceEquipment(player);

                        this.lastRefreshTime = curTime;
                    }

                    // limit toggle refresh rate
                    if(DateTimeOffset.UtcNow - lastToggleRefresh > TimeSpan.FromMilliseconds(250))
                    {
                        PersistToggleStates();
                        lastToggleRefresh = DateTimeOffset.UtcNow;
                    }

                    // Velocity reading randomly hangs game ???
                    //UpdateStates(player);
                }

            }

            void UpdateStates(nint player)
            {
                if (this.allocator == null)
                    return;

                if (velocityLocation == nint.MaxValue)
                    velocityLocation = this.allocator.Allocate(sizeof(float) * 4);

                this.Instance.Engine.ObjectGetVelocity(velocityLocation, player);

                Span<float> v = stackalloc float[3];
                this.Instance.RemoteProcess.ReadSpanAt<float>(velocityLocation, v);

                this.PlayerVelocity = $"<{v[0]:0.0}, {v[1]:0.0}, {v[2]:0.0}>";
            }

            async Task EnforceEquipment(nint player)
            {
                var p = this.Persistence.GetEquipementRefreshPersistence();

                var walllevel = p.First(e => e.KeyName == "schematic_wall");

                if (walllevel.GlobalValue != 0)
                {
                    // we should have dropwall
                    var dropwallIndex = this.Instance.Engine.Unit_GetEquipmentIndexByAbilityType(player, 0);

                    if (dropwallIndex == -1)
                    {
                        // we don't have a dropwall tho

                        await this.Persistence.RefreshEquipment();
                    }
                }
            }

            void PersistToggleStates()
            {
                foreach (var item in AllActionItems.Where(i => i.IsToggleAction))
                {
                    var gameState = item.GetToggleState();

                    if (item.ToggleState != gameState)
                    {
                        // Force the game into our state if we should and we're not in cutscenes or whatever
                        if (this.ShouldPersistToggles)
                        {
                            if (this.ProbablyInGame && !this.InCutscene)
                                item.InvokeRaw();
                        }
                        else
                        {
                            item.ToggleState = gameState; // otherwise reset us to reality
                        }
                    }
                }
            }
        }

        public void ToggleShouldPersistToggles()
        {
            this.ShouldPersistToggles = !this.ShouldPersistToggles;
        }

        public void ToggleShouldEnforceEquipment()
        {
            this.ShouldEnforceEquipment = !this.ShouldEnforceEquipment;
        }

        public void ClearInfiniteCreds()
        {
            CredentialStore.ClearInfiniteCredentials();
        }

        public async Task ImportInfiniteCreds()
        {
            var storage = TopLevel.GetTopLevel(App.PrimaryWindow).StorageProvider;
            var results = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select credentials to load",
                SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(Environment.CurrentDirectory),
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Infinite Credentials File"){ Patterns = new string[] { "*.infcred" } }
                }
            });

            var result = results.FirstOrDefault();

            if (result != null)
            {
                CredentialStore.LoadInfiniteCredentials(result.TryGetLocalPath());
            }
        }

        public async void RefreshEquipment()
        {
            await this.Persistence.RefreshEquipment();
        }

        public void ExportInfiniteCreds()
        {
            CredentialStore.SaveInfiniteCredentials();
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

            uint? loadedMapId = null;
            uint? loadedSpawnId = null;

            if (result != null)
            {
                using var file = await result.OpenReadAsync();
                var prog = ProgressionData.FromStream(file);

                loadedMapId = prog.Entries.FirstOrDefault(e => e.KeyName == PersistenceKeys.CampfireMapId)?.GlobalValue;
                loadedSpawnId = prog.Entries.FirstOrDefault(e => e.KeyName == PersistenceKeys.CampfireSpawnId)?.GlobalValue;

                this.Persistence.SetProgress(prog.Entries);
            }

            if (this.MapResetOnLoad)
                this.Instance.ResetOrLaunchMap(loadedMapId, loadedSpawnId);
        }
    }
}
