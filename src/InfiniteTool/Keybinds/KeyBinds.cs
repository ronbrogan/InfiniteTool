using Avalonia.Controls;
using Avalonia.Input;
using HarfBuzzSharp;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace InfiniteTool.Keybinds
{
    public interface IBindableUiAction
    {
        string Id { get; set; }
        string Label { get; set; }
        string KeyBind { get; set; }

        Task Invoke();
    }

    [AddINotifyPropertyChangedInterface]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public abstract class BindableUiAction : IBindableUiAction
    {
        public string Id { get; set; }

        public string Label { get; set; }
        public string? KeyBind { get; set; }

        [DependsOn(nameof(Label), nameof(KeyBind))]
        public string LabelAndBinding => Label + (KeyBind == null ? "" : " <" + KeyBind + ">");

        public string Tooltip { get; set; } = "Right click for binding options";

        public Func<bool> IsEnabled { get; set; }

        public abstract Task Invoke();

        public ContextMenu BindingContextMenu { get; set; }
    }

    public static class KeyBinds
    {
        private const string BindingConfigFile = "keybinds.cfg";

        private static Dictionary<string, (ModifierKeys mods, Key key)> bindings = new();

        private static Hotkeys hotkeys = new();
        private static Window window;

        static KeyBinds()
        {
            LoadBindings();
        }

        public static void Initialize(Window window)
        {
            KeyBinds.window = window;
        }

        public static void SetupActions(IEnumerable<BindableUiAction> actions)
        {
            foreach(var action in actions)
            {
                SetupAction(action);
            }
        }

        public static void SetupAction(BindableUiAction action)
        {
            if (bindings.TryGetValue(action.Id, out var binding))
            {
                if (hotkeys.TryRegisterHotKey(binding.mods, binding.key, () => Task.Run(action.Invoke)))
                {
                    action.KeyBind = Hotkeys.KeyToString(binding.mods, binding.key);
                }
            }

            action.BindingContextMenu = BuildMenu(action);
        }

        public static ContextMenu BuildMenu(IBindableUiAction action)
        {
            var menu = new ContextMenu();
            var unbind = new MenuItem() { Header = "Unbind" };
            var bind = new MenuItem() { Header = "Bind" };

            bind.CommandParameter = action;
            bind.Command = ReactiveUI.ReactiveCommand.Create<IBindableUiAction>(Bind_OnClick);

            unbind.CommandParameter = action;
            unbind.Command = ReactiveUI.ReactiveCommand.Create<IBindableUiAction>(Unbind_OnClick);

            menu.Items.Add(bind);
            menu.Items.Add(unbind);

            return menu;
        }

        private static void Unbind_OnClick(IBindableUiAction action)
        {
            if (action == null) return;
            
            if (bindings.TryGetValue(action.Id, out var binding))
            {
                hotkeys.UnregisterHotKey(binding.mods, binding.key);
                action.KeyBind = null;
                bindings.Remove(action.Id);
                SaveBindings();
            }
        }

        private static async void Bind_OnClick(IBindableUiAction action)
        {
            if (action == null) return;

            var dialog = new KeyBindDialog();
            await dialog.ShowDialog(window);

            if (dialog.DialogResult && dialog.Data.MainKey != Key.None)
            {
                if (bindings.TryGetValue(action.Id, out var binding))
                {
                    hotkeys.UnregisterHotKey(binding.mods, binding.key);
                    action.KeyBind = null;
                }

                if (hotkeys.TryRegisterHotKey(dialog.Data.ModifierKeys, dialog.Data.MainKey, () => Task.Run(action.Invoke)))
                {
                    action.KeyBind = Hotkeys.KeyToString(dialog.Data.ModifierKeys, dialog.Data.MainKey);
                    bindings[action.Id] = (dialog.Data.ModifierKeys, dialog.Data.MainKey);
                    SaveBindings();
                }
            }
            
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
