using InfiniteTool.WPF;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InfiniteTool.Keybinds
{
    public static class KeyBinds
    {
        private static Dictionary<string, (ModifierKeys mods, Key key)> bindings = new()
        {
            ["cp"] = (ModifierKeys.None, Key.F9),
            ["revert"] = (ModifierKeys.None, Key.F10),
            ["toggleCheckpointSuppression"] = (ModifierKeys.None, Key.F11)
        };

        private static ModifierKeys NoRepeat = (ModifierKeys)0x4000;

        public static void Initialize(Window window, Hotkeys hotkeys)
        {
            var bindables = window.FindChildren<Button>(b => b.Name.StartsWith("bindable_"));
            var ctxMenu = BuildMenu();

            foreach (var bindable in bindables)
            {
                bindable.Tag = new BindableInfo(bindable.Content as string, hotkeys, bindings);
                bindable.ContextMenu = ctxMenu;
                bindable.ToolTip = "Right click for binding options";

                if (bindings.TryGetValue(bindable.Name.Substring("bindable_".Length), out var binding))
                {
                    if (hotkeys.TryRegisterHotKey(binding.mods | NoRepeat, binding.key, () => bindable.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))))
                    {
                        bindable.Content = bindable.Content + " <" + Hotkeys.KeyToString(binding.mods, binding.key) + ">";
                    }
                }
            }
        }

        private static ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            var unbind = new MenuItem() { Header = "Unbind" };
            var bind = new MenuItem() { Header = "Bind" };

            bind.CommandParameter = menu;
            bind.Click += Bind_OnClick;

            unbind.CommandParameter = menu;
            unbind.Click += Unbind_OnClick;

            menu.Items.Add(bind);
            menu.Items.Add(unbind);

            return menu;
        }

        private static void Unbind_OnClick(object sender, RoutedEventArgs e)
        {
            var bindable = FindClickedItem(sender);
            if (bindable != null && bindable.Tag is BindableInfo info)
            {
                if (info.Bindings.TryGetValue(bindable.Name.Substring("bindable_".Length), out var binding))
                {
                    info.Hotkeys.UnregisterHotKey(binding.mods, binding.key);
                    bindable.Content = info.Text;
                }
            }
        }

        private static void Bind_OnClick(object sender, RoutedEventArgs e)
        {
            var bindable = FindClickedItem(sender);
            if (bindable != null && bindable.Tag is BindableInfo info)
            {
                // do something to get new binding
                var dialog = new KeyBindDialog();
                if (dialog.ShowDialog() ?? false && dialog.MainKey != Key.None)
                {
                    var bindableName = bindable.Name.Substring("bindable_".Length);
                    if (info.Bindings.TryGetValue(bindableName, out var binding))
                    {
                        info.Hotkeys.UnregisterHotKey(binding.mods, binding.key);
                        bindable.Content = info.Text;
                    }

                    if (info.Hotkeys.TryRegisterHotKey(dialog.ModifierKeys, dialog.MainKey, () => bindable.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))))
                    {
                        bindable.Content = info.Text + " <" + Hotkeys.KeyToString(dialog.ModifierKeys, dialog.MainKey) + ">";
                        info.Bindings[bindableName] = (dialog.ModifierKeys, dialog.MainKey);
                    }
                }
            }
        }

        private static Button? FindClickedItem(object sender)
        {
            var mi = sender as MenuItem;
            if (mi == null)
            {
                return null;
            }

            var cm = mi.CommandParameter as ContextMenu;
            if (cm == null)
            {
                return null;
            }

            return cm.PlacementTarget as Button;
        }

        private class BindableInfo
        {
            public BindableInfo(string? content, Hotkeys hotkeys, Dictionary<string, (ModifierKeys mods, Key key)> bindings)
            {
                this.Text = content;
                this.Hotkeys = hotkeys;
                this.Bindings = bindings;
            }

            public string? Text { get; set; }
            public Hotkeys Hotkeys { get; set; }
            public Dictionary<string, (ModifierKeys mods, Key key)> Bindings { get; set; }
        }
    }
}
