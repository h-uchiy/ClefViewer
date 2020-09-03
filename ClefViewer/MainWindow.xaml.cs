using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClefViewer.Properties;

namespace ClefViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(sender is ListBox listBox))
            {
                return;
            }

            // Show selected row when selected row was changed from view model
            if (0 < listBox.SelectedItems.Count)
            {
                var selectedItem = listBox.SelectedItems[0];
                if (selectedItem != null)
                {
                    listBox.ScrollIntoView(selectedItem);
                }
            }

            e.Handled = true;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.Render = Settings.Default.Render;
                viewModel.Unescape = Settings.Default.Unescape;
                viewModel.Unwrap = Settings.Default.Unwrap;
                viewModel.AutoReload = Settings.Default.AutoReload;
                viewModel.Tail = Settings.Default.Tail;
                viewModel.TailSize = Settings.Default.TailSize;
                viewModel.LogFilePath = Settings.Default.LogFilePath;
            }
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                Settings.Default.Render = viewModel.Render;
                Settings.Default.Unescape = viewModel.Unescape;
                Settings.Default.Unwrap = viewModel.Unwrap;
                Settings.Default.AutoReload = viewModel.AutoReload;
                Settings.Default.Tail = viewModel.Tail;
                Settings.Default.TailSize = viewModel.TailSize;
                if (string.IsNullOrEmpty(viewModel.LogFilePath) || File.Exists(viewModel.LogFilePath))
                {
                    Settings.Default.LogFilePath = viewModel.LogFilePath;
                }
            }

            // Dispose view model
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}