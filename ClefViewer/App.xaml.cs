using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using ClefViewer.Properties;

namespace ClefViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += (sender, args) =>
            {
                // TODO: Write to Serilog
                Console.Error.WriteLine(args.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                // TODO: Write to Serilog
                Console.Error.WriteLine(args.ExceptionObject);
            };
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                // TODO: Write to Serilog
                Console.Error.WriteLine(args.Exception);
            };
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            retry:
            try
            {
                Settings.Default.Save();
            }
            catch (ConfigurationErrorsException exception)
            {
                var messageBoxResult = MessageBox.Show(
                    $"{exception.GetBaseException().Message} Retry?",
                    Settings.Default.AppName, MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    goto retry;
                }
            }
        }
    }
}