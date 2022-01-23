using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace InfiniteTool
{
    public sealed class Hotkeys : IDisposable
    {
        private const int WmHotKey = 0x0312;
        private readonly ILogger? logger;
        private readonly WindowsNative hotkeyInterop;
        private readonly WindowInteropHelper interop;
        private readonly HashSet<int> identifiers = new();
        private readonly Dictionary<(ModifierKeys mods, Key key), int> registeredKeys = new();
        private readonly Dictionary<int, Action> keyCallbacks = new();
        private readonly Random random = new Random();
        private bool disposedValue;

        public Hotkeys(Window window, ILogger? logger = null)
        { 
            this.interop = new WindowInteropHelper(window);

            ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
            this.logger = logger;
            this.hotkeyInterop = new WindowsNative();
        }

        public bool TryRegisterHotKey(ModifierKeys modifiers, Key key, Action callback)
        {
            var identifier = random.Next();

            while(!this.identifiers.Add(identifier))
            {
                unchecked { identifier++; }
            }

            if(this.registeredKeys.TryAdd((modifiers, key), identifier))
            {
                if(this.hotkeyInterop.RegisterHotKey(this.interop.Handle, identifier, modifiers, KeyInterop.VirtualKeyFromKey(key)))
                {
                    this.keyCallbacks.Add(identifier, callback);
                    this.logger?.LogInformation("Registered binding: {key} with ID: {id}", KeyToString(modifiers, key), identifier);
                    return true; 
                }
            }

            this.logger?.LogInformation("Unable to register binding: {key} with ID: {id}", KeyToString(modifiers, key), identifier);
            this.identifiers.Remove(identifier);
            this.registeredKeys.Remove((modifiers, key));
            return false;
        }

        public void UnregisterHotKey(ModifierKeys modifiers, Key key)
        {
            if(!this.registeredKeys.TryGetValue((modifiers, key), out var id))
            {
                return;
            }

            this.hotkeyInterop.UnregisterHotKey(this.interop.Handle, id);

            this.registeredKeys.Remove((modifiers, key));
            this.keyCallbacks.Remove(id);
            this.identifiers.Remove(id);

            this.logger?.LogInformation("Unregistered binding: {key} with ID: {id}", KeyToString(modifiers, key), id);
        }

        public static string KeyToString(ModifierKeys modifiers, Key key)
        {
            var builder = new StringBuilder();

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                builder.Append("CTRL+");
            }
            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                builder.Append("SHIFT+");
            }
            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                builder.Append("ALT+");
            }
            if (modifiers.HasFlag(ModifierKeys.Windows))
            {
                builder.Append("WIN+");
            }
            builder.Append(key.ToString());

            return builder.ToString();
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled) return;

            if(msg.message == WmHotKey && this.keyCallbacks.TryGetValue((int)msg.wParam, out var cb))
            {
                try
                {
                    cb?.Invoke();
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex, "Hotkey exception for ID: {id}", msg.wParam);
                }

                handled = true;
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                foreach (var id in this.identifiers)
                {
                    this.hotkeyInterop.UnregisterHotKey(this.interop.Handle, id);
                }

                if (disposing)
                {
                    this.hotkeyInterop.Dispose();
                }

                this.identifiers.Clear();
                this.keyCallbacks.Clear();
                this.registeredKeys.Clear();

                disposedValue = true;
            }
        }

        ~Hotkeys()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private unsafe sealed class WindowsNative : IDisposable
        {
            // Obfuscated import names to attempt to make av not be overzealous
            // obfuscation via ("0x" + string.Join(", 0x", Encoding.UTF32.GetBytes("RegisterHotKey").Select(b => b ^ 0x77).Select(b => b.ToString("x2")))).Dump();
            private byte[] libraryString = new byte[] { 0x02, 0x77, 0x77, 0x77, 0x04, 0x77, 0x77, 0x77, 0x12, 0x77, 0x77, 0x77, 0x05, 0x77, 0x77, 0x77, 0x44, 0x77, 0x77, 0x77, 0x45, 0x77, 0x77, 0x77, 0x59, 0x77, 0x77, 0x77, 0x13, 0x77, 0x77, 0x77, 0x1b, 0x77, 0x77, 0x77, 0x1b, 0x77, 0x77, 0x77 };
            private byte[] registerString = new byte[] { 0x25, 0x77, 0x77, 0x77, 0x12, 0x77, 0x77, 0x77, 0x10, 0x77, 0x77, 0x77, 0x1e, 0x77, 0x77, 0x77, 0x04, 0x77, 0x77, 0x77, 0x03, 0x77, 0x77, 0x77, 0x12, 0x77, 0x77, 0x77, 0x05, 0x77, 0x77, 0x77, 0x3f, 0x77, 0x77, 0x77, 0x18, 0x77, 0x77, 0x77, 0x03, 0x77, 0x77, 0x77, 0x3c, 0x77, 0x77, 0x77, 0x12, 0x77, 0x77, 0x77, 0x0e, 0x77, 0x77, 0x77 };
            private byte[] unregisterString = new byte[] { 0x22, 0x77, 0x77, 0x77, 0x19, 0x77, 0x77, 0x77, 0x05, 0x77, 0x77, 0x77, 0x12, 0x77, 0x77, 0x77, 0x10, 0x77, 0x77, 0x77, 0x1e, 0x77, 0x77, 0x77, 0x04, 0x77, 0x77, 0x77, 0x03, 0x77, 0x77, 0x77, 0x12, 0x77, 0x77, 0x77, 0x05, 0x77, 0x77, 0x77, 0x3f, 0x77, 0x77, 0x77, 0x18, 0x77, 0x77, 0x77, 0x03, 0x77, 0x77, 0x77, 0x3c, 0x77, 0x77, 0x77, 0x12, 0x77, 0x77, 0x77, 0x0e, 0x77, 0x77, 0x77 };


            private bool disposedValue;
            private IntPtr user32;
            delegate* unmanaged<IntPtr, int, ModifierKeys, int, bool> register;
            delegate* unmanaged<IntPtr, int, bool> unregister;

            public WindowsNative()
            {
                this.user32 = NativeLibrary.Load(ToString(libraryString), Assembly.GetExecutingAssembly(), null);
                this.register = (delegate* unmanaged<IntPtr, int, ModifierKeys, int, bool>)NativeLibrary.GetExport(user32, ToString(registerString));
                this.unregister = (delegate* unmanaged<IntPtr, int, bool>)NativeLibrary.GetExport(user32, ToString(unregisterString));
            }

            public bool RegisterHotKey(IntPtr hWnd, int id, ModifierKeys fsModifiers, int vk)
                => this.register(hWnd, id, fsModifiers, vk);

            public bool UnregisterHotKey(IntPtr hWnd, int id)
                => this.unregister(hWnd, id);

            private void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        NativeLibrary.Free(this.user32);
                        this.user32 = IntPtr.Zero;
                        this.register = null;
                        this.unregister = null;
                    }

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
            }

            private string ToString(byte[] b)
            {
                return Encoding.UTF32.GetString(b.Select(b => (byte)(b ^ 0x77)).ToArray());
            }
        }
    }
}
