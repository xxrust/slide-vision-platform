using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp2.UI.Controls
{
    public partial class ImageInspectionViewer : UserControl
    {
        private readonly ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new TranslateTransform();
        private readonly TransformGroup _transformGroup = new TransformGroup();
        private Point? _lastDragPoint;

        private BitmapSource _bitmap;
        private byte[] _pixelBuffer;
        private int _stride;

        public ImageInspectionViewer()
        {
            InitializeComponent();

            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
            ImageElement.RenderTransform = _transformGroup;

            ImageCanvas.MouseWheel += ImageCanvas_MouseWheel;
            ImageCanvas.MouseLeftButtonDown += ImageCanvas_MouseLeftButtonDown;
            ImageCanvas.MouseLeftButtonUp += ImageCanvas_MouseLeftButtonUp;
            ImageCanvas.MouseMove += ImageCanvas_MouseMove;
        }

        public void LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Clear();
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

                _bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                _stride = _bitmap.PixelWidth * 4;
                _pixelBuffer = new byte[_stride * _bitmap.PixelHeight];
                _bitmap.CopyPixels(_pixelBuffer, _stride, 0);

                ImageElement.Source = _bitmap;
                ResetTransform();
                UpdateInfoText(null);
            }
            catch
            {
                Clear();
            }
        }

        public void Clear()
        {
            _bitmap = null;
            _pixelBuffer = null;
            _stride = 0;
            ImageElement.Source = null;
            ResetTransform();
            InfoTextBlock.Text = "--";
        }

        private void ResetTransform()
        {
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
        }

        private void ImageCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_bitmap == null)
            {
                return;
            }

            double zoom = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            Point position = e.GetPosition(ImageCanvas);

            double newScale = Math.Max(0.1, Math.Min(50, _scaleTransform.ScaleX * zoom));
            zoom = newScale / _scaleTransform.ScaleX;

            _translateTransform.X = position.X - zoom * (position.X - _translateTransform.X);
            _translateTransform.Y = position.Y - zoom * (position.Y - _translateTransform.Y);

            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;
        }

        private void ImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastDragPoint = e.GetPosition(ImageCanvas);
            ImageCanvas.CaptureMouse();
        }

        private void ImageCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _lastDragPoint = null;
            ImageCanvas.ReleaseMouseCapture();
        }

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_lastDragPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(ImageCanvas);
                Vector delta = position - _lastDragPoint.Value;
                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;
                _lastDragPoint = position;
            }

            UpdateInfoText(e);
        }

        private void UpdateInfoText(MouseEventArgs e)
        {
            if (_bitmap == null)
            {
                InfoTextBlock.Text = "--";
                return;
            }

            if (e == null)
            {
                InfoTextBlock.Text = $"Size: {_bitmap.PixelWidth}x{_bitmap.PixelHeight}";
                return;
            }

            Point position = e.GetPosition(ImageCanvas);
            double scale = _scaleTransform.ScaleX;
            double x = (position.X - _translateTransform.X) / scale;
            double y = (position.Y - _translateTransform.Y) / scale;

            int ix = (int)Math.Floor(x);
            int iy = (int)Math.Floor(y);

            if (ix >= 0 && iy >= 0 && ix < _bitmap.PixelWidth && iy < _bitmap.PixelHeight)
            {
                int index = iy * _stride + ix * 4;
                byte b = _pixelBuffer[index];
                byte g = _pixelBuffer[index + 1];
                byte r = _pixelBuffer[index + 2];
                InfoTextBlock.Text = $"X:{ix} Y:{iy}  R:{r} G:{g} B:{b}";
            }
            else
            {
                InfoTextBlock.Text = $"X:{ix} Y:{iy}";
            }
        }
    }
}
