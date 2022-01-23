using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
