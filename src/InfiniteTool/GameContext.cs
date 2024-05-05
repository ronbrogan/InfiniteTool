﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using InfiniteTool.Extensions;
using InfiniteTool.Formats;
using InfiniteTool.GameInterop;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using static InfiniteTool.GameInterop.GamePersistence;

namespace InfiniteTool
{
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
    public class ActionItem : BindableUiAction
    {
        private readonly Action action;
        private Func<bool>? toggleStatusFunc;

        private Brush defaultBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255));
        private Brush toggledOnBrush = new SolidColorBrush(Color.FromArgb(128, 2, 120, 255));

        public IReactiveCommand InvokeCommand { get; set; }

        public Expression<Func<GameContext, bool>>? Guard { get; set;}

        public ActionItem(string label, string id, Action action, Func<bool>? toggleStatusFunc = null)
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

        public override void Invoke()
        {
            if (!this.IsEnabled()) return;

            InvokeRaw();

            if (toggleStatusFunc != null)
                ToggleState = GetToggleState();
        }

        public void InvokeRaw()
        {
            try
            {
                action();
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

        public TagInfo? SelectedWeapon { get; set; }
        public TagInfo? SelectedVehicle { get; set; }
        public TagInfo? SelectedBiped { get; set; }

        public ObservableCollection<SkullActionItem> Skulls { get; set; }

        public ObservableCollection<ActionItem> Actions { get; set; }

        public ObservableCollection<ActionItem> Hacks { get; set; }

        public bool ProbablyInGame { get; set; }
        public bool Paused { get; set; }
        public bool InCutscene { get; set; }
        public bool HasProcess { get; set; }

        public bool MapResetOnLoad { get; set; } = true;

        public bool ShouldPersistToggles { get; set; }

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
                new("Show Coords", "coords", Instance.RestockPlayer),
            };

            Hacks = new()
            {
                new("No clip", "noclip", Instance.TogglePlayerNoClip, Instance.PlayerNoClip),
                new("Slow-mo", "slowMoGuys", Instance.ToggleSlowMo, Instance.SlowMoActivated),
                new("Fast-mo", "fastMoGals", Instance.ToggleFastMo, Instance.FastMoActivated),

                new("Stop Time", "pause", Instance.ToggleGameTimePause, Instance.GameTimeIsPaused),
                new("Suspend AI","aiSuspend", Instance.ToggleAi, Instance.AiDisabled),
                new("Nuke All AI","aiNuke", Instance.NukeAi),

                new("Toggle Invuln","invuln", Instance.ToggleInvuln, Instance.PlayerIsInvulnerable),
                new("Restock", "restock", Instance.RestockPlayer),


                new("Easy Diff", "makeEasy", Instance.TriggerCheckpoint),
                new("Leg Diff", "makeLeg", Instance.TriggerCheckpoint),

            };

            // Register all current action items with hotkeys, etc
            WireUpActions();

            instance.OnAttach += Instance_OnAttach;
            instance.BeforeDetach += Instance_BeforeDetach;
        }

        private void Instance_BeforeDetach(object? sender, EventArgs e)
        {
            enforceTogglesCts.Cancel();
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
                this.LoadToggleStates();
                await EnforceToggleStates();
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
        private CancellationTokenSource enforceTogglesCts = new();
        private async Task EnforceToggleStates()
        {
            enforceTogglesCts = new CancellationTokenSource();

            await Task.Delay(250);

            while (!enforceTogglesCts.IsCancellationRequested)
            {
                this.ProbablyInGame = Instance.ProbablyIsInGame();
                this.Paused = Instance.IsPaused();
                this.InCutscene = Instance.InCutscene();

                foreach (var item in AllActionItems.Where(i => i.IsToggleAction))
                {
                    var gameState = item.GetToggleState();

                    if (item.ToggleState != gameState)
                    {
                        // Force the game into our state if we should and we're not in cutscenes or whatever
                        if (this.ShouldPersistToggles)
                        {
                            if(this.ProbablyInGame && !this.InCutscene)
                                item.InvokeRaw();
                        }
                        else
                        {
                            item.ToggleState = gameState; // otherwise reset us to reality
                        }
                    }
                }

                await Task.Delay(250);
            }
        }

        public void ToggleShouldPersistToggles()
        {
            this.ShouldPersistToggles = !this.ShouldPersistToggles;
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
