using System.Windows;
using System.Windows.Controls;

namespace ClefViewer
{
    public class TextBoxWithFileDrop : TextBox
    {
        protected override void OnDragEnter(DragEventArgs e)
        {
            if (AcceptFileDrop(e)) return;
            base.OnDragEnter(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (AcceptFileDrop(e)) return;
            base.OnDragOver(e);
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            if (AcceptFileDrop(e)) return;
            base.OnDragLeave(e);
        }

        private static bool AcceptFileDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return true;
            }

            return false;
        }
    }
}