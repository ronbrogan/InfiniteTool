using Grpc.Core;
using InfiniteTool.GameInterop;
using InfiniteTool.GameInterop.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Superintendent.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace InfiniteTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost _host;

        public string LogLocation;

        public App()
        {
            var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Environment.CurrentDirectory = assemblyDir;

            LogLocation = Path.Combine(Environment.CurrentDirectory, "log.txt");

            var serilogLogger = new LoggerConfiguration()
                .WriteTo.File(LogLocation)
                .CreateLogger();

            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                if (e.Exception is TaskCanceledException || e.Exception is OperationCanceledException)
                {
                    // ignore task canceled, it's expected to happen :)
                    return;
                }

                if(e.Exception is RpcException rpce && rpce.StatusCode == StatusCode.Cancelled)
                {
                    // ignore canceled RPCs, it's expected to happen :)
                    return;
                }

                serilogLogger.Error(e.Exception.ToString());
            };

            Console.SetOut(new TextWriterLogger(serilogLogger));

            serilogLogger.Information("AppInfo: " + Assembly.GetExecutingAssembly().ToString());
            

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<Hotkeys>(s => s.GetRequiredService<MainWindow>().Hotkeys);
                    services.AddSingleton<IOffsetProvider, JsonOffsetProvider>();
                    services.AddSingleton<GameContext>();
                    services.AddSingleton<GameInstance>();
                    services.AddSingleton<GamePersistence>();
                })
                .ConfigureLogging(l => l.AddSerilog(serilogLogger))
                .Build();
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            await this.StartupImpl();
        }

        private async Task StartupImpl()
        {
            await _host.StartAsync();

            var siLogger = _host.Services.GetRequiredService<ILogger<Superintendent.Core.Tracer>>();
            SuperintendentLog.UseLogger(siLogger);

            var game = _host.Services.GetRequiredService<GameInstance>();
            var persistence = _host.Services.GetRequiredService<GamePersistence>();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            _ = Task.Run(() =>
            {
                game.Initialize();
            });
        }

        public static void RestartAsAdmin()
        {
            var proc = Process.GetCurrentProcess().MainModule.FileName;

            var info = new ProcessStartInfo(proc);
            info.UseShellExecute = true;
            info.Verb = "runas";

            try
            {
                var asAdmin = Process.Start(info);
                Process.GetCurrentProcess().Kill();
            }
            catch (Win32Exception wex)
            {
                if (wex.NativeErrorCode == 0x4C7/*ERROR_CANCELLED*/)
                {
                    MessageBox.Show("Cannot attach to the game, it's likely running as Admin and this tool is not.", "Infinite Tool Error");
                    Process.GetCurrentProcess().Kill();
                }
                else
                {
                    throw;
                }
            }
        }

        private async void Application_Exit(object sender, ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
        }

        public class TextWriterLogger : TextWriter
        {
            private Logger logger;
            private StringBuilder builder = new StringBuilder();
            private bool terminatorStarted = false;

            public TextWriterLogger(Logger log)
            {
                logger = log;
            }

            public override void Write(string? value)
            {
                logger.Information(value);
            }

            public override void Write(char value)
            {
                builder.Append(value);
                if (value == NewLine[0])
                    if (NewLine.Length == 1)
                        Flush();
                    else
                        terminatorStarted = true;
                else if (terminatorStarted)
                    if (terminatorStarted = NewLine[1] == value)
                        Flush();
            }

            private void Flush()
            {
                if (builder.Length > NewLine.Length)
                    logger.Debug(builder.ToString());
                builder.Clear();
                terminatorStarted = false;
            }


            public override Encoding Encoding
            {
                get { return Encoding.Default; }
            }
        }
    }
}
