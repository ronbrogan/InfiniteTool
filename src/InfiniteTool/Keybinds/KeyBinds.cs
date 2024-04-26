using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InfiniteTool.Keybinds
{
    public static class KeyBinds
    {
        private const string BindingConfigFile = "keybinds.cfg";

        private static Dictionary<string, (ModifierKeys mods, Key key)> bindings = new();


        public static void Initialize(Window window, Hotkeys hotkeys)
        {
            LoadBindings();

            var bindables = window.GetSelfAndLogicalDescendants()
                .OfType<Button>()
                .Where(b => b.Name != null && b.Name.StartsWith("bindable_"))
                .ToHashSet();

            var ctxMenu = BuildMenu();

            foreach (var bindable in bindables)
            {
                if (bindable.Tag is BindableInfo) continue; // already setup
                
                var info = new BindableInfo(bindable.Content as string, () => bindable.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), hotkeys, bindings);
                bindable.Tag = info;
                bindable.ContextMenu = ctxMenu;
                ToolTip.SetTip(bindable, "Right click for binding options");
            
                if (bindings.TryGetValue(bindable.Name.Substring("bindable_".Length), out var binding))
                {
                    if (hotkeys.TryRegisterHotKey(binding.mods | ModifierKeys.NoRepeat, binding.key, info.Action))
                    {
                        bindable.Content = bindable.Content + " <" + Hotkeys.KeyToString(binding.mods, binding.key) + ">";
                    }
                }
            }
        }

        public static (ContextMenu, object?) SetupBinding(string label, string id, Action action, Hotkeys hotkeys)
        {
            LoadBindings();

            var tag = new BindableInfo(label, action, hotkeys, bindings);
            var ctx = BuildMenu();

            if (bindings.TryGetValue(id.Substring("bindable_".Length), out var binding))
            {
                if (hotkeys.TryRegisterHotKey(binding.mods | ModifierKeys.NoRepeat, binding.key, action))
                {
                    tag.TextAndBinding = tag.Text + " <" + Hotkeys.KeyToString(binding.mods, binding.key) + ">";
                }
            }

            return (ctx, tag);
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

        private static void Unbind_OnClick(object? sender, RoutedEventArgs e)
        {
            var bindable = FindClickedItem(sender);
            if (bindable != null && bindable.Tag is BindableInfo info)
            {
                var bindableName = bindable.Name.Substring("bindable_".Length);
                if (info.Bindings.TryGetValue(bindableName, out var binding))
                {
                    info.Hotkeys.UnregisterHotKey(binding.mods, binding.key);
                    info.TextAndBinding = info.Text;
                    info.Bindings.Remove(bindableName);
                    SaveBindings();
                }
            }
        }

        private static async void Bind_OnClick(object? sender, RoutedEventArgs e)
        {
            var bindable = FindClickedItem(sender);
            if (bindable != null && bindable.Tag is BindableInfo info)
            {
                // do something to get new binding
                var dialog = new KeyBindDialog();

                await dialog.ShowDialog((Window)TopLevel.GetTopLevel(bindable));

                if (dialog.DialogResult && dialog.Data.MainKey != Key.None)
                {
                    var bindableName = bindable.Name.Substring("bindable_".Length);
                    if (info.Bindings.TryGetValue(bindableName, out var binding))
                    {
                        info.Hotkeys.UnregisterHotKey(binding.mods, binding.key);
                        info.TextAndBinding = info.Text;
                    }

                    if (info.Hotkeys.TryRegisterHotKey(dialog.Data.ModifierKeys, dialog.Data.MainKey, info.Action))
                    {
                        info.TextAndBinding = info.Text + " <" + Hotkeys.KeyToString(dialog.Data.ModifierKeys, dialog.Data.MainKey) + ">";
                        info.Bindings[bindableName] = (dialog.Data.ModifierKeys, dialog.Data.MainKey);
                        SaveBindings();
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

            return cm.FindLogicalAncestorOfType<Button>();
        }

        [AddINotifyPropertyChangedInterface]
        public class BindableInfo
        {
            public BindableInfo(string? content, Action action, Hotkeys hotkeys, Dictionary<string, (ModifierKeys mods, Key key)> bindings)
            {
                this.Text = this.TextAndBinding = content;
                this.Action = action;
                this.Hotkeys = hotkeys;
                this.Bindings = bindings;
            }

            public string? Text { get; set; }
            public string TextAndBinding { get; set; }
            public Action Action { get; }
            public Hotkeys Hotkeys { get; set; }
            public Dictionary<string, (ModifierKeys mods, Key key)> Bindings { get; set; }

            public string Tooltip => "Right click for binding options";
        }

        private static void LoadBindings()
        {
            if (bindings.Count > 0) return;

            try
            {
                var lines = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, BindingConfigFile));

                foreach(var line in lines)
                {
                    var parts = line.Split("|");
                    bindings[parts[0]] = ((ModifierKeys)Convert.ToInt32(parts[1]), (Key)Convert.ToInt32(parts[2]));
                }
            }
            catch 
            {
                bindings["cp"] = (ModifierKeys.None, Key.F9);
                bindings["revert"] = (ModifierKeys.None, Key.F10);
                bindings["toggleCheckpointSuppression"] = (ModifierKeys.None, Key.F11);
            }
        }

        private static void SaveBindings()
        {
            try
            {
                var lines = new List<string>();

                foreach (var (name, bindInfo) in bindings)
                {
                    lines.Add($"{name}|{(int)bindInfo.mods}|{(int)bindInfo.key}");
                }

                File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, BindingConfigFile), lines);
            }
            catch { }
        }
    }
}
