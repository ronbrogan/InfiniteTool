using InfiniteTool.Formats;
using InfiniteTool.GameInterop;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace InfiniteTool
{

    [AddINotifyPropertyChangedInterface]
    public partial class MainWindow : Window
    {
        public readonly Hotkeys Hotkeys;
        private readonly ILogger<MainWindow> logger;

        public GameContext Game { get; set; }

        public MainWindow(GameContext context, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            this.Game = context;
            this.Hotkeys = new Hotkeys(this, logger);
            this.DataContext = context;
            this.logger = logger;
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            KeyBinds.Initialize(this, Hotkeys);
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
            this.Game.SaveCurrentCheckpoint();
        }

        private void suppressCp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.ToggleCheckpointSuppression();
        }

        private void doubleRevert_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.DoubleRevert();
        }

        private void invulnToggle_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.ToggleInvuln();
        }

        private void coordsToggle_Click(object sender, RoutedEventArgs e)
        {
        }

        private void flycamToggle_Click(object sender, RoutedEventArgs e)
        {
        }


        private void refreshPersistence_Click(object sender, RoutedEventArgs e)
        {
            this.Game.RefreshPersistence();
        }

        private void startLevel_Click(object sender, RoutedEventArgs e)
        {
            this.Game.StartSelectedLevel();
        }

        private void injectCp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.InjectSelectedCheckpoint();
        }
        
        private void restock_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.RestockPlayer();
        }

        private void saveCp_Click(object sender, RoutedEventArgs e)
        {
            var save = new SaveFileDialog();
            save.DefaultExt = ".infcp";
            save.AddExtension = true;
            save.FileName = "checkpoint.infcp";
            save.Filter = "Infinite Checkpoint Files (*.infcp) | *.infcp";
            if(save.ShowDialog(this) ?? false)
            {
                var cp = this.Game.SelectedCheckpoint;
                using var file = save.OpenFile();
                file.Write(cp.Data);

                this.Game.SelectedCheckpoint.Filename = save.FileName;
            }
        }

        //private void loadCp_Click(object sender, RoutedEventArgs e)
        //{
        //    var open = new OpenFileDialog();
        //    open.DefaultExt = ".infcp";
        //    open.AddExtension = true;
        //    open.FileName = "checkpoint.infcp";
        //    open.Filter = "Infinite Checkpoint Files (*.infcp) | *.infcp";
        //    if (open.ShowDialog(this) ?? false)
        //    {
        //        using var file = open.OpenFile();
        //        var cpData = new byte[GameInstance.CheckpointDataSize];
        //        file.Read(cpData);
        //
        //        this.Game.AddCheckpoint(cpData, open.FileName);
        //    }
        //}

        private void aboutMenu_Click(object sender, RoutedEventArgs e)
        {
            var about = new About();
            about.Show();
        }

        private void saveProgression_Click(object sender, RoutedEventArgs e)
        {
            var save = new SaveFileDialog();
            save.DefaultExt = ".infprog";
            save.AddExtension = true;
            save.FileName = "progress.infprog";
            save.DereferenceLinks = false;
            save.Filter = "Infinite Progress Files (*.infprog) | *.infprog";
            if (save.ShowDialog(this) ?? false)
            {
                using var file = save.OpenFile();
                var progress = new ProgressionData(this.Game.Persistence.CurrentParticipantId, this.Game.PersistenceEntries);
                progress.Write(file);
            }
        }

        private void loadProgression_Click(object sender, RoutedEventArgs e)
        {
            var open = new OpenFileDialog();
            open.DefaultExt = ".infprog";
            open.AddExtension = true;
            open.FileName = "progress.infprog";
            open.DereferenceLinks = false;
            open.Filter = "Infinite Progress Files (*.infprog) | *.infprog";
            if (open.ShowDialog(this) ?? false)
            {
                using var file = open.OpenFile();
                var data = ProgressionData.FromStream(file);

                if (data == null) return;

                this.Game.Persistence.SetProgress(data.Entries);
            }
        }

        private void speedrunPostLights_Click(object sender, RoutedEventArgs e)
        {
            var gameVersion = Game.Instance.GetGameVersion();
            if (gameVersion == null) return;

            var progPath = Path.Combine(Environment.CurrentDirectory, "Data", gameVersion, "lightskip.infprog");
            if (!File.Exists(progPath)) return;

            var file = File.OpenRead(progPath);
            var data = ProgressionData.FromStream(file);
            if (data == null) return;

            this.Game.Persistence.SetProgress(data.Entries);
        }

        private void openLogLocation_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", string.Format("/select,\"{0}\"", (Application.Current as App).LogLocation));
        }

        private void ejectMombasa_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ejecting from the process will cause this tool to cease functioning until the game or the tool is restarted");
            this.Game.Instance.RemoteProcess.EjectMombasa();
        }
    }
}
