using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace InfiniteTool
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine(e.Exception);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}
