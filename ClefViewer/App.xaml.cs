using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ClefViewer.Properties;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ClefViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += (obj, args) => Log.Fatal(args.Exception, "{@Dispatcher} UnhandledException", obj);
            AppDomain.CurrentDomain.UnhandledException += (obj, args) => Log.Fatal(args.ExceptionObject as Exception, "{@AppDomain} UnhandledException", obj);
            TaskScheduler.UnobservedTaskException += (obj, args) => Log.Fatal(args.Exception, "{@TaskScheduler} UnhandledException", obj);

            var appName = Assembly.GetExecutingAssembly().GetName().Name;
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName, $"{appName}.clef");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(new CompactJsonFormatter(), filePath, LogEventLevel.Verbose, rollingInterval: RollingInterval.Day, buffered: true)
                .CreateLogger();

            Log.Information("Application start.");
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                retry:
                try
                {
                    Settings.Default.Save();
                }
                catch (ConfigurationErrorsException exception)
                {
                    var messageBoxResult = MessageBox.Show($"{exception.GetBaseException().Message} Retry?", Settings.Default.AppName, MessageBoxButton.YesNo);
                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        goto retry;
                    }
                }
            }
            finally
            {
                Log.Information("Application exit with code {ExitCode}.", e.ApplicationExitCode);
                Log.CloseAndFlush();
            }
        }
    }
}