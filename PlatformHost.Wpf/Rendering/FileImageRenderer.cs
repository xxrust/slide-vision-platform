using System.IO;
using System.Windows;

namespace WpfApp2.Rendering
{
    public sealed class FileImageRenderer : IImageRenderer
    {
        private ImageRendererContext _context;

        public string RendererId => ImageRendererIds.File;

        public void Bind(ImageRendererContext context)
        {
            _context = context;
            ShowPreviewImages();
        }

        public void DisplayImageGroup(WpfApp2.UI.ImageGroupSet group)
        {
            if (_context == null)
            {
                return;
            }

            ShowPreviewImages();

            SetImageSource(_context.PreviewViewer1, group?.GetPath(0));
            SetImageSource(_context.PreviewViewer2, group?.GetPath(1));
            SetImageSource(_context.PreviewViewer3, group?.GetPath(2));
            SetImageSource(_context.PreviewViewer4, group?.GetPath(3));
            SetImageSource(_context.PreviewViewer5, group?.GetPath(4));
            SetImageSource(_context.PreviewViewer6, group?.GetPath(5));
            SetImageSource(_context.PreviewViewer7, group?.GetPath(6));
            SetImageSource(_context.PreviewViewer8, group?.GetPath(7));
            SetImageSource(_context.PreviewViewer9, group?.GetPath(8));
            SetImageSource(_context.PreviewViewer10, group?.GetPath(9));
        }

        public void Clear()
        {
            if (_context == null)
            {
                return;
            }

            SetImageSource(_context.PreviewViewer1, null);
            SetImageSource(_context.PreviewViewer2, null);
            SetImageSource(_context.PreviewViewer3, null);
            SetImageSource(_context.PreviewViewer4, null);
            SetImageSource(_context.PreviewViewer5, null);
            SetImageSource(_context.PreviewViewer6, null);
            SetImageSource(_context.PreviewViewer7, null);
            SetImageSource(_context.PreviewViewer8, null);
            SetImageSource(_context.PreviewViewer9, null);
            SetImageSource(_context.PreviewViewer10, null);
        }

        private void ShowPreviewImages()
        {
            if (_context == null)
            {
                return;
            }

            if (_context.PreviewViewer1 != null) _context.PreviewViewer1.Visibility = Visibility.Visible;
            if (_context.PreviewViewer2 != null) _context.PreviewViewer2.Visibility = Visibility.Visible;
            if (_context.PreviewViewer3 != null) _context.PreviewViewer3.Visibility = Visibility.Visible;
            if (_context.PreviewViewer4 != null) _context.PreviewViewer4.Visibility = Visibility.Visible;
            if (_context.PreviewViewer5 != null) _context.PreviewViewer5.Visibility = Visibility.Visible;
            if (_context.PreviewViewer6 != null) _context.PreviewViewer6.Visibility = Visibility.Visible;
            if (_context.PreviewViewer7 != null) _context.PreviewViewer7.Visibility = Visibility.Visible;
            if (_context.PreviewViewer8 != null) _context.PreviewViewer8.Visibility = Visibility.Visible;
            if (_context.PreviewViewer9 != null) _context.PreviewViewer9.Visibility = Visibility.Visible;
            if (_context.PreviewViewer10 != null) _context.PreviewViewer10.Visibility = Visibility.Visible;
        }

        private static void SetImageSource(WpfApp2.UI.Controls.ImageInspectionViewer target, string path)
        {
            if (target == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                target.Clear();
                return;
            }

            target.LoadImage(path);
        }
    }
}

