using Microsoft.Extensions.Logging;
using mrousavy;
using System;
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

        public GameContext GameContext { get; set; }

        public MainWindow(GameContext context, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            this.GameContext = context;
            this.DataContext = context;
            this.logger = logger;
        }

        protected override void OnActivated(EventArgs e)
        {
            try
            {
                if(this.CheckpointHotkey == null)
                {
                    this.CheckpointHotkey = new HotKey(ModifierKeys.None, Key.F9, this, h => this.GameContext.TriggerCheckpoint());
                    this.CheckpointBind = "Binding: F9";
                }
            }
            catch { }

            try
            {
                if(this.RevertHotkey == null)
                {
                    this.RevertHotkey = new HotKey(ModifierKeys.None, Key.F10, this, h => this.GameContext.TriggerRevert());
                    this.RevertBind = "Binding: F10";
                }
            }
            catch { }

            base.OnInitialized(e);
        }

        private void cp_Click(object sender, RoutedEventArgs e)
        {
            this.GameContext.TriggerCheckpoint();
        }

        private void revert_Click(object sender, RoutedEventArgs e)
        {
            this.GameContext.TriggerRevert();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            this.CheckpointHotkey?.Dispose();
            this.RevertHotkey?.Dispose();
            base.OnClosing(e);
        }
    }
}
