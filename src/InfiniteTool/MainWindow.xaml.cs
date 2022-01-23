using InfiniteTool.GameInterop;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PropertyChanged;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace InfiniteTool
{
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindow : Window
    {
        public readonly Hotkeys Hotkeys;
        private readonly ILogger<MainWindow> logger;

        public List<GamePersistence.Entry> PersistenceEntries { get; private set; } = new();

        public GameContext Game { get; set; }

        public CheckpointData SelectedCheckpoint { get; set; }

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
