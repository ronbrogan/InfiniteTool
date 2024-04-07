using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Win32.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace InfiniteTool
{
    public enum ModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
        NoRepeat = 0x4000
    }

    public sealed class Hotkeys : IDisposable
    {
        private const int WmHotKey = 0x0312;
        private const int WmRegisterHotKey = 0x8000;
        private const int WmUnRegisterHotKey = 0x8001;

        private readonly ILogger? logger;
        private readonly WindowsNative hotkeyInterop;
        private readonly HashSet<int> identifiers = new();
        private readonly Dictionary<(ModifierKeys mods, Key key), int> registeredKeys = new();
        private readonly Dictionary<int, Action> keyCallbacks = new();
        private readonly Random random = new Random();
        private bool disposedValue;

        private Thread workThread;
        private uint workThreadId;

        public Hotkeys(ILogger? logger = null)
        {
            this.logger = logger;
            this.hotkeyInterop = new WindowsNative();

            if(!Design.IsDesignMode)
            {
                this.workThread = new Thread(ThreadWork)
                {
                    IsBackground = true
                };
                this.workThread.Start(this);
            }
        }

        public ConcurrentDictionary<int, (ManualResetEventSlim, bool)> operations = new();
        public bool TryRegisterHotKey(ModifierKeys modifiers, Key key, Action callback)
        {
            var identifier = random.Next();

            while (!this.identifiers.Add(identifier))
            {
                unchecked { identifier++; }
            }

            if (this.registeredKeys.TryAdd((modifiers, key), identifier))
            {
                var mre = new ManualResetEventSlim();
                operations.TryAdd(identifier, (mre, false));

                var lp = (nint)modifiers << 32 | KeyInterop.VirtualKeyFromKey(key);
                PInvoke.PostThreadMessage(workThreadId, WmRegisterHotKey, new WPARAM((nuint)identifier), new LPARAM(lp));

                mre.Wait();
                operations.TryRemove(identifier, out var result);

                if (result.Item2)
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
            if (!this.registeredKeys.TryGetValue((modifiers, key), out var id))
            {
                return;
            }

            var mre = new ManualResetEventSlim();
            operations.TryAdd(id, (mre, false));

            PInvoke.PostThreadMessage(workThreadId, WmUnRegisterHotKey, new WPARAM((nuint)id), new LPARAM());

            mre.Wait();
            operations.TryRemove(id, out var result);

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

        
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
                this.workThread.Join();

                if (disposing)
                {
                    this.hotkeyInterop.Dispose();
                }

                this.identifiers.Clear();
                this.keyCallbacks.Clear();
                this.registeredKeys.Clear();
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

        private static void ThreadWork(object state)
        {
            var hk = (Hotkeys)state;

            hk.workThreadId = PInvoke.GetCurrentThreadId();

            while (!hk.disposedValue)
            {
                var result = PInvoke.GetMessage(out var message, HWND.Null, 0, 0);

                switch(message.message)
                {
                    case WmHotKey:
                        ProcessKey(message);
                        break;
                    case WmRegisterHotKey:
                        RegisterKey(message);
                        break;
                    case WmUnRegisterHotKey:
                        UnRegisterKey(message);
                        break;
                }
            }

            foreach (var id in hk.identifiers)
            {
                hk.hotkeyInterop.UnregisterHotKey(0, id);
            }

            void ProcessKey(MSG message)
            {
                if (hk.keyCallbacks.TryGetValue((int)message.wParam.Value, out var cb))
                {
                    try
                    {
                        cb?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        hk.logger?.LogError(ex, "Hotkey exception for ID: {id}", message.wParam);
                    }
                }
            }

            void RegisterKey(MSG message)
            {
                var id = (int)message.wParam.Value;
                var vk = (int)message.lParam.Value;
                var mods = (ModifierKeys)(message.lParam.Value >> 32);
                var result = hk.hotkeyInterop.RegisterHotKey(0, id, mods, vk);

                if(hk.operations.TryGetValue(id, out var op))
                {
                    hk.operations[id] = (op.Item1, result);
                    op.Item1.Set();
                }
            }

            void UnRegisterKey(MSG message)
            {
                var id = (int)message.wParam.Value;
                var result = hk.hotkeyInterop.UnregisterHotKey(0, id);

                if (hk.operations.TryGetValue(id, out var op))
                {
                    hk.operations[id] = (op.Item1, result);
                    op.Item1.Set();
                }
            }
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
