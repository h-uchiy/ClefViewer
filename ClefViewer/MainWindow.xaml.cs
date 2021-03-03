using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
            
            ViewModel.PropertyChanged += (sender, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(ViewModel.SelectedFilterMethods):
                    case nameof(ViewModel.SelectedLevelIndex):
                        Dispatcher.InvokeAsync(ShowSelectedRow, DispatcherPriority.Background);
                        break;
                }
            };
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (!ReferenceEquals(sender, ListBox))
            {
                return;
            }

            ViewModel.SelectedLogRecords = ListBox.SelectedItems.Cast<LogRecord>().ToList();

            // Show selected row when selected row was changed from view model
            ShowSelectedRow();

            e.Handled = true;
        }
        
        private void ShowSelectedRow()
        {
            if (0 < ListBox.SelectedItems.Count)
            {
                var selectedItem = ListBox.SelectedItems[0];
                if (selectedItem != null)
                {
                    ListBox.ScrollIntoView(selectedItem);
                }
            }
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