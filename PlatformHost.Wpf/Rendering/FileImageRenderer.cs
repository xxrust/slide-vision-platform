using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

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

            SetImageSource(_context.PreviewImage1, group?.Source1Path);
            SetImageSource(_context.PreviewImage2_1, group?.Source2_1Path);
            SetImageSource(_context.PreviewImage2_2, group?.Source2_2Path);
        }

        public void Clear()
        {
            if (_context == null)
            {
                return;
            }

            SetImageSource(_context.PreviewImage1, null);
            SetImageSource(_context.PreviewImage2_1, null);
            SetImageSource(_context.PreviewImage2_2, null);
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

            if (_context.PreviewImage1 != null) _context.PreviewImage1.Visibility = Visibility.Visible;
            if (_context.PreviewImage2_1 != null) _context.PreviewImage2_1.Visibility = Visibility.Visible;
            if (_context.PreviewImage2_2 != null) _context.PreviewImage2_2.Visibility = Visibility.Visible;
        }

        private static void SetImageSource(System.Windows.Controls.Image target, string path)
        {
            if (target == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                target.Source = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                target.Source = bitmap;
            }
            catch
            {
                target.Source = null;
            }
        }
    }
}
