using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PropertyChanged;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InfiniteTool.Keybinds
{
    [AddINotifyPropertyChangedInterface]
    public class KeyBindViewModel
    {
        public ModifierKeys ModifierKeys { get; set; }

        public Key MainKey { get; set; }

        public bool CanSave => MainKey != Key.None;

        public string? BindingString { get; set; }

        public bool Committed = false;

        public void KeyBindDialog_KeyUp(object? sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (this.Committed) return;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                this.ModifierKeys &= ~ModifierKeys.Control;
            }
            else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                this.ModifierKeys &= ~ModifierKeys.Shift;
            }
            else if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                this.ModifierKeys &= ~ModifierKeys.Alt;
            }
            else if (e.Key == Key.LWin || e.Key == Key.RWin)
            {
                this.ModifierKeys &= ~ModifierKeys.Windows;
            }
            this.BindingString = Hotkeys.KeyToString(this.ModifierKeys, this.MainKey);
        }

        public void KeyBindDialog_KeyDown(object? sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (this.Committed)
            {
                this.ModifierKeys = ModifierKeys.None;
            }

            this.Committed = false;
            this.MainKey = 0;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                this.ModifierKeys |= ModifierKeys.Control;
            }
            else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                this.ModifierKeys |= ModifierKeys.Shift;
            }
            else if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                this.ModifierKeys |= ModifierKeys.Alt;
            }
            else if (e.Key == Key.LWin || e.Key == Key.RWin)
            {
                this.ModifierKeys |= ModifierKeys.Windows;
            }
            else
            {
                if (e.Key == Key.System)
                {
                    // todo: broke on avalonia port
                    this.MainKey = e.Key;
                }
                else
                {
                    this.MainKey = e.Key;
                }
                this.Committed = true;
            }

            this.BindingString = Hotkeys.KeyToString(this.ModifierKeys, this.MainKey);
        }
    }

    /// <summary>
    /// Interaction logic for KeyBindDialog.xaml
    /// </summary>
    [DoNotNotify]
    public partial class KeyBindDialog : Window
    {
        public KeyBindViewModel Data { get; set; }

        public bool DialogResult { get; set; }

        public KeyBindDialog()
        {
            InitializeComponent();
            this.Data = new();
            this.DataContext = this.Data;
            
            this.bindingTarget.AddHandler(KeyDownEvent, Data.KeyBindDialog_KeyDown, RoutingStrategies.Tunnel);
            this.bindingTarget.AddHandler(KeyUpEvent, Data.KeyBindDialog_KeyUp, RoutingStrategies.Tunnel);

            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.CanResize = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            this.bindingTarget.Focusable = true;
            this.bindingTarget.Focus(NavigationMethod.Pointer);
            this.bindingTarget.LostFocus += BindingTarget_LostFocus;
            base.OnLoaded(e);
        }

        private void BindingTarget_LostFocus(object? sender, RoutedEventArgs e)
        {
            this.bindingTarget.Focus(NavigationMethod.Pointer);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
