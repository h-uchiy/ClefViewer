using System.Windows;
using System.Windows.Controls;

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

            // Show selected row
            if(0 < listBox.SelectedItems.Count)
            {
                var selectedItem = listBox.SelectedItems[0];
                if (selectedItem != null)
                {
                    listBox.ScrollIntoView(selectedItem);
                }
            }
            
            e.Handled = true;
        }
    }
}