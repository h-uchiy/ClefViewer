using System.Configuration;
using System.Windows;
using ClefViewer.Properties;

namespace ClefViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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