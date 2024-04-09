using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using InfiniteTool.Keybinds;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace InfiniteTool
{

    [DoNotNotify]
    public partial class MainWindow : Window
    {
        public readonly Hotkeys Hotkeys;
        private readonly ILogger<MainWindow> logger;

        public GameContext Game { get; set; }

        public MainWindow(GameContext context, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            this.Game = context;
            this.Hotkeys = new Hotkeys(logger);
            this.DataContext = context;
            this.logger = logger;
            this.Loaded += MainWindow_Loaded;

            this.PointerMoved += InputElement_OnPointerMoved;
            this.PointerPressed += InputElement_OnPointerPressed;
            this.PointerReleased += InputElement_OnPointerReleased;

            
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
       

        private void suppressCp_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.ToggleCheckpointSuppression();
        }

        private void doubleRevert_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.DoubleRevert();
        }

        private void invulnToggle_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.ToggleInvuln();
        }

        private void points_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.SetEquipmentPoints(45);
        }

        private void equipment_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.UnlockAllEquipment();
        }

        private void equipmentReset_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.ResetAllEquipment();
        }

        private void weapon_Click(object sender, RoutedEventArgs e)
        {
            if (this.Game.SelectedWeapon == null)
                return;

            this.Game.Instance.SpawnWeapon(this.Game.SelectedWeapon);
        }

        private void vehicle_Click(object sender, RoutedEventArgs e)
        {
            if (this.Game.SelectedVehicle == null)
                return;

            this.Game.Instance.SpawnVehicle(this.Game.SelectedVehicle);
        }

        private void biped_Click(object sender, RoutedEventArgs e)
        {
            if (this.Game.SelectedBiped == null)
                return;

            this.Game.Instance.SpawnBiped(this.Game.SelectedBiped);
        }

        private void skull_Click(object sender, RoutedEventArgs e)
        {

        }

        private void refreshPersistence_Click(object sender, RoutedEventArgs e)
        {
            this.Game.RefreshPersistence();
        }

        private void stopTime_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.TogglePause();
        }

        private void suspendAi_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.ToggleAi();
        }

        private void nukeAi_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.NukeAi();
        }

        private void coordsToggle_Click(object sender, RoutedEventArgs e)
        {
        }

        private void flycamToggle_Click(object sender, RoutedEventArgs e)
        {
        }

        private async void saveProgression_Click(object sender, RoutedEventArgs e)
        {
            await this.Game.SavePersistence(this);
        }

        private void restock_Click(object sender, RoutedEventArgs e)
        {
            this.Game.Instance.RestockPlayer();
        }

        private void aboutMenu_Click(object sender, RoutedEventArgs e)
        {
            var about = new About();
            about.Show();
        }

        private void openLogLocation_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", string.Format("/select,\"{0}\"", (Application.Current as App).LogLocation));
        }

        private void ejectMombasa_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("Ejecting from the process will cause this tool to cease functioning until the game or the tool is restarted");
            this.Game.Instance.RemoteProcess.EjectMombasa();
        }

        private void replInvoke_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fnptr = GetValueFromHexBox("func");
                var arg0 = GetValueFromHexBox("arg0");
                var arg1 = GetValueFromHexBox("arg1");
                var arg2 = GetValueFromHexBox("arg2");
                var arg3 = GetValueFromHexBox("arg3");

                var proc = this.Game.Instance.RemoteProcess;

                this.Game.Instance.PrepareForScriptCalls();

                var sw = Stopwatch.StartNew();
                var result = proc.CallFunction<nint>(fnptr.Value, arg0, arg1, arg2, arg3);

                SetResult($"Invoke response after {sw.ElapsedMilliseconds}ms\r\n{result.Item2:x16}");
            }
            catch(Exception ex)
            {
                SetResult("Exception Cought\r\n" + ex.ToString());
            }

            nint? GetValueFromHexBox(string name)
            {
                var box = this.Find<TextBox>(name);

                if (box == null)
                    throw new Exception($"Couldn't find textbox '{name}'");

                if (string.IsNullOrWhiteSpace(box.Text))
                    return null;

                return (nint)Convert.ToInt64(box.Text, 16);
            }

            void SetResult(string content)
            {
                this.Find<TextBox>("replResult").Text = content;
            }
        }

        private void writeInvoke_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data1 = GetDataFromHexBox("data1");
                var data2 = GetDataFromHexBox("data2");

                var proc = this.Game.Instance.RemoteProcess;

                var len = 65535;

                if(data1 != null)
                    len += data1.Length;

                if (data2 != null)
                    len += data2.Length;

                if(len == 0)
                {
                    SetResult("Nothing written");
                    return;
                }

                var basis = proc.Allocate(len);

                var sw = Stopwatch.StartNew();
                var sb = new StringBuilder();

                if(data1 != null)
                {
                    proc.WriteSpanAt<byte>(basis, data1);
                    sb.Append($"Wrote Data1 - {data1.Length} bytes at {basis:x}");
                    basis += data1.Length;
                }

                if (data2 != null)
                {
                    proc.WriteSpanAt<byte>(basis, data2);
                    sb.Append($"Wrote Data2 - {data2.Length} bytes at {basis:x}");
                    basis += data2.Length;
                }

                SetResult(sb.ToString());
            }
            catch (Exception ex)
            {
                SetResult("Exception Cought\r\n" + ex.ToString());
            }

            byte[]? GetDataFromHexBox(string name)
            {
                var box = this.Find<TextBox>(name);

                if (box == null)
                    throw new Exception($"Couldn't find textbox '{name}'");

                if (string.IsNullOrWhiteSpace(box.Text))
                    return null;


                return BitConverter.GetBytes((nint)Convert.ToInt64(box.Text, 16));
            }

            void SetResult(string content)
            {
                this.Find<TextBox>("writeResult").Text = content;
            }
        }

        private void readInvoke_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addr = GetDataFromHexBox("data3");

                var proc = this.Game.Instance.RemoteProcess;

                var len = 64;


                var sw = Stopwatch.StartNew();

                var data = new byte[len];
                proc.ReadSpanAt<byte>(addr, data);

                SetResult(Convert.ToHexString(data));
            }
            catch (Exception ex)
            {
                SetResult("Exception Cought\r\n" + ex.ToString());
            }

            nint GetDataFromHexBox(string name)
            {
                var box = this.Find<TextBox>(name);

                if (box == null)
                    throw new Exception($"Couldn't find textbox '{name}'");

                if (string.IsNullOrWhiteSpace(box.Text))
                    return 0;


                return (nint)Convert.ToInt64(box.Text, 16);
            }

            void SetResult(string content)
            {
                this.Find<TextBox>("readResult").Text = content;
            }
        }

        private async void dumpExe_Click(object sender, RoutedEventArgs e)
        {
            var proc = this.Game.Instance.RemoteProcess;

            var start = 0;
            var size = 75_002_792;//proc.Process.MainModule.ModuleMemorySize;

            var result = await GetTopLevel(this).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save location"
            });

            if(result != null)
            {
                using var file = await result.OpenWriteAsync();

                WriteTo(file);
            }

            void WriteTo(Stream file)
            {
                var buf = new byte[4096].AsSpan();

                try
                {
                    proc.SuspendAppThreads();
                    while (size > 0)
                    {
                        var count = Math.Min(size, 4096);
                        var slice = buf.Slice(0, count);

                        proc.ReadSpan(start, slice);
                        file.Write(slice);

                        size -= count;
                        start += count;
                    }
                }
                catch
                {

                }
                finally
                {
                    proc.ResumeAppThreads();
                }


                file.Close();
            }
        }

        private bool _mouseDownForWindowMoving = false;
        private PointerPoint _originalPoint;

        private void InputElement_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_mouseDownForWindowMoving) return;

            PointerPoint currentPoint = e.GetCurrentPoint(this);
            Position = new PixelPoint(Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
                Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y));
        }

        private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is not Border b) return;

            if (b.Parent is not Menu) return;

            if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen) return;

            _mouseDownForWindowMoving = true;
            _originalPoint = e.GetCurrentPoint(this);
        }

        private void InputElement_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _mouseDownForWindowMoving = false;
        }
    }
}
