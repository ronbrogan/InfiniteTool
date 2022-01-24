using InfiniteTool.GameInterop;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PropertyChanged;
using System.IO;
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
            save.Filter = "Infinite Checkpoint Files (*.infcp) | *.infcp";
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
            open.Filter = "Infinite Checkpoint Files (*.infcp) | *.infcp";
            if (open.ShowDialog(this) ?? false)
            {
                using var file = open.OpenFile();
                var cpData = new byte[GameInstance.CheckpointDataSize];
                file.Read(cpData);

                this.Game.AddCheckpoint(cpData, open.FileName);
            }
        }

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
                using var writer = new StreamWriter(file);
                writer.WriteLine("InfiniteProgressV1");
                writer.WriteLine($"ParticipantID:0x{this.Game.Persistence.CurrentParticipantId:X}");
                writer.WriteLine("KeyName,DataType,GlobalValue,ParticipantValue");
                foreach (var entry in this.Game.PersistenceEntries)
                {
                    writer.WriteLine($"{entry.KeyName},{entry.DataType},0x{entry.GlobalValue:X},0x{entry.ParticipantValue:X}");
                }
            }
        }

        private void speedrunPostLights_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
