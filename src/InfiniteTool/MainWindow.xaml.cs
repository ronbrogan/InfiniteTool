using InfiniteTool.GameInterop;
using Microsoft.Extensions.Logging;
using mrousavy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace InfiniteTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ILogger<MainWindow> logger;
        private HotKey? CheckpointHotkey;
        private HotKey? RevertHotkey;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string CheckpointBind { get; private set; }
        public string RevertBind { get; private set; }


        public List<GamePersistence.Entry> PersistenceEntries { get; private set; }

        public GameContext Game { get; set; }

        public MainWindow(GameContext context, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            this.Game = context;
            this.DataContext = context;
            this.logger = logger;

            this.PersistenceEntries = new List<GamePersistence.Entry>()
            {
                new () { KeyName = "TestKeyBool", GlobalValue = 1, ParticipantValue = 0 },
                new () { KeyName = "TestKeyByte", GlobalValue = 0x0a, ParticipantValue = 0x0a },
                new () { KeyName = "TestKeyLong", GlobalValue = 0x123123, ParticipantValue = 0x123123 },
            };
        }

        protected override void OnActivated(EventArgs e)
        {
            try
            {
                if(this.CheckpointHotkey == null)
                {
                    this.CheckpointHotkey = new HotKey(ModifierKeys.None, Key.F9, this, h => this.Game.Instance.TriggerCheckpoint());
                    this.CheckpointBind = "Binding: F9";
                }
            }
            catch { }

            try
            {
                if(this.RevertHotkey == null)
                {
                    this.RevertHotkey = new HotKey(ModifierKeys.None, Key.F10, this, h => this.Game.Instance.TriggerRevert());
                    this.RevertBind = "Binding: F10";
                }
            }
            catch { }

            base.OnInitialized(e);
        }

        private void cp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.TriggerCheckpoint();
        }

        private void revert_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.TriggerRevert();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            this.CheckpointHotkey?.Dispose();
            this.RevertHotkey?.Dispose();
            base.OnClosing(e);
        }

        private void refreshPersistence_Click(object sender, RoutedEventArgs e)
        {
            this.PersistenceEntries = this.Game.Persistence.GetAllProgress();
        }
    }
}
