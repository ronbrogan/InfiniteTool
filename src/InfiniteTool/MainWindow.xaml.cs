using InfiniteTool.GameInterop;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace InfiniteTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly Hotkeys hotkeys;
        private readonly ILogger<MainWindow> logger;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string CheckpointBind { get; private set; }
        public string RevertBind { get; private set; }
        public string KeepCpBind { get; private set; }
        public string SuppressBind { get; private set; }


        public List<GamePersistence.Entry> PersistenceEntries { get; private set; } = new();

        public GameContext Game { get; set; }

        public CheckpointData SelectedCheckpoint { get; set; }

        public MainWindow(GameContext context, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            this.Game = context;
            this.hotkeys = new Hotkeys(this, logger);
            this.DataContext = context;
            this.logger = logger;
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.hotkeys.TryRegisterHotKey(ModifierKeys.None, Key.F9, () => this.Game.Instance.TriggerCheckpoint()))
            {
                this.CheckpointBind = "Binding: " + Hotkeys.KeyToString(ModifierKeys.None, Key.F9);
            }

            if (this.hotkeys.TryRegisterHotKey(ModifierKeys.None, Key.F10, () => this.Game.Instance.TriggerRevert()))
            {
                this.RevertBind = "Binding: " + Hotkeys.KeyToString(ModifierKeys.None, Key.F10);
            }

            if (this.hotkeys.TryRegisterHotKey(ModifierKeys.None, Key.F11, () => this.Game.Instance.ToggleCheckpointSuppression()))
            {
                this.SuppressBind = "Binding: " + Hotkeys.KeyToString(ModifierKeys.None, Key.F11);
            }
        }

        private void cp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.TriggerCheckpoint();
        }

        private void revert_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.TriggerRevert();
        }
        
        private void keepCp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.SaveCheckpoint();
        }

        private void suppressCp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.ToggleCheckpointSuppression();
        }

        private void refreshPersistence_Click(object sender, RoutedEventArgs e)
        {
            this.PersistenceEntries = this.Game.Persistence.GetAllProgress();
        }

        private void startLevel_Click(object sender, RoutedEventArgs e)
        {
            this.Game.StartSelectedLevel();
        }

        private void injectCp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.InjectCheckpoint(SelectedCheckpoint.Data);
        }

        private void saveCp_Click(object sender, RoutedEventArgs e)
        {
            var save = new SaveFileDialog();
            save.DefaultExt = ".infcp";
            save.AddExtension = true;
            save.FileName = "checkpoint.infcp";
            if(save.ShowDialog(this) ?? false)
            {
                var cp = this.SelectedCheckpoint;
                using var file = save.OpenFile();
                file.Write(cp.Data);

                this.SelectedCheckpoint.Filename = save.FileName;
            }
        }

        private void loadCp_Click(object sender, RoutedEventArgs e)
        {
            var open = new OpenFileDialog();
            open.DefaultExt = ".infcp";
            open.AddExtension = true;
            open.FileName = "checkpoint.infcp";
            if (open.ShowDialog(this) ?? false)
            {
                using var file = open.OpenFile();
                var cpData = new byte[GameInstance.CheckpointDataSize];
                file.Read(cpData);

                this.Game.Instance.AddCheckpoint(cpData, open.FileName);
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)}\r\nCopyright 2022, Helical Software, LLC.\r\n Uses open source libraries. Full details, source, and downloads found at \r\n https://github.com/ronbrogan/InfiniteTool", 
                "About Infinite Tool");
        }
    }
}
