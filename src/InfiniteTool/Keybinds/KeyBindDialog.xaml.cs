using PropertyChanged;
using System.Windows;
using System.Windows.Input;

namespace InfiniteTool.Keybinds
{
    /// <summary>
    /// Interaction logic for KeyBindDialog.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class KeyBindDialog : Window
    {
        public ModifierKeys ModifierKeys { get; set; }
        public Key MainKey { get; set; }

        public bool CanSave => MainKey != Key.None;

        public string BindingString { get; set; }
        private bool committed = false;

        public KeyBindDialog()
        {
            InitializeComponent();
            this.DataContext = this;
            this.KeyDown += KeyBindDialog_KeyDown;
            this.KeyUp += KeyBindDialog_KeyUp;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.ResizeMode = ResizeMode.NoResize;
        }

        private void KeyBindDialog_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (committed) return;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                ModifierKeys &= ~ModifierKeys.Control;
            }
            else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                ModifierKeys &= ~ModifierKeys.Shift;
            }
            else if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                ModifierKeys &= ~ModifierKeys.Alt;
            }
            else if (e.Key == Key.LWin || e.Key == Key.RWin)
            {
                ModifierKeys &= ~ModifierKeys.Windows;
            }

            this.BindingString = Hotkeys.KeyToString(ModifierKeys, MainKey);
        }

        private void KeyBindDialog_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if(committed)
            {
                ModifierKeys = ModifierKeys.None;
            }

            committed = false;
            MainKey = 0;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                ModifierKeys |= ModifierKeys.Control;
            }
            else if(e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                ModifierKeys |= ModifierKeys.Shift;
            }
            else if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                ModifierKeys |= ModifierKeys.Alt;
            }
            else if(e.Key == Key.LWin || e.Key == Key.RWin)
            {
                ModifierKeys |= ModifierKeys.Windows;
            }
            else
            {
                if(e.Key == Key.System)
                {
                    MainKey = e.SystemKey;
                }
                else
                {
                    MainKey = e.Key;
                }

                committed = true;
            }

            this.BindingString = Hotkeys.KeyToString(ModifierKeys, MainKey);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
