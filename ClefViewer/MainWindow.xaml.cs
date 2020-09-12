using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClefViewer.Properties;
using Serilog;

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

            ViewModel.SelectedLogRecords = listBox.SelectedItems.Cast<LogRecord>().ToList();

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

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            // Dispose view model
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private void MainWindow_OnDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            Log.Information("MainWindow_OnDrop() files={@Files}", files);
            if (files != null && files.Length > 0 && !string.IsNullOrWhiteSpace(files[0]))
            {
                ViewModel.LogFilePath = files[0];
            }
        }

        private void MainWindow_OnPreviewDrop(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop, false)
                ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void ListView_CopyCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (ViewModel.CopyCommand.CanExecute("LeftPane"))
            {
                ViewModel.CopyCommand.Execute("LeftPane");
            }
        }
    }
}