using InfiniteTool.GameInterop;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PropertyChanged;
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

        private void saveCp_Click(object sender, RoutedEventArgs e)
        {
            var save = new SaveFileDialog();
            save.DefaultExt = ".infcp";
            save.AddExtension = true;
            save.FileName = "checkpoint.infcp";
            if(save.ShowDialog(this) ?? false)
            {
                var cp = this.Game.SelectedCheckpoint;
                using var file = save.OpenFile();
                file.Write(cp.Data);

                this.Game.SelectedCheckpoint.Filename = save.FileName;
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

                this.Game.AddCheckpoint(cpData, open.FileName);
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var about = new About();
            about.Show();
        }
    }
}
