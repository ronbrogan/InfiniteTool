using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using System;
using System.IO;
using System.Text;
using System.Windows;

namespace InfiniteTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost _host;

        public App()
        {
            var serilogLogger = new LoggerConfiguration()
                .WriteTo.File("log.txt")
                .CreateLogger();

            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                serilogLogger.Error(e.Exception.ToString());
            };

            Console.SetOut(new TextWriterLogger(serilogLogger));

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<GameContext>();
                })
                .ConfigureLogging(l => l.AddSerilog(serilogLogger))
                .Build();
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();

            var game = _host.Services.GetRequiredService<GameContext>();
            await game.Initialize();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
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

            public override void Write(string value)
            {
                logger.Debug(value);
            }

            public override void Write(char value)
            {
                builder.Append(value);
                if (value == NewLine[0])
                    if (NewLine.Length == 1)
                        Flush2Log();
                    else
                        terminatorStarted = true;
                else if (terminatorStarted)
                    if (terminatorStarted = NewLine[1] == value)
                        Flush2Log();
            }

            private void Flush2Log()
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
