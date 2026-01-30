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

            SetImageSource(_context.PreviewViewer1, group?.Source1Path);
            SetImageSource(_context.PreviewViewer2_1, group?.Source2_1Path);
            SetImageSource(_context.PreviewViewer2_2, group?.Source2_2Path);
        }

        public void Clear()
        {
            if (_context == null)
            {
                return;
            }

            SetImageSource(_context.PreviewViewer1, null);
            SetImageSource(_context.PreviewViewer2_1, null);
            SetImageSource(_context.PreviewViewer2_2, null);
        }

        private void ShowPreviewImages()
        {
            if (_context == null)
            {
                return;
            }

            if (_context.VmRender1 != null) _context.VmRender1.Visibility = Visibility.Collapsed;
            if (_context.VmRender2_1 != null) _context.VmRender2_1.Visibility = Visibility.Collapsed;
            if (_context.VmRender2_2 != null) _context.VmRender2_2.Visibility = Visibility.Collapsed;

            if (_context.PreviewViewer1 != null) _context.PreviewViewer1.Visibility = Visibility.Visible;
            if (_context.PreviewViewer2_1 != null) _context.PreviewViewer2_1.Visibility = Visibility.Visible;
            if (_context.PreviewViewer2_2 != null) _context.PreviewViewer2_2.Visibility = Visibility.Visible;
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
