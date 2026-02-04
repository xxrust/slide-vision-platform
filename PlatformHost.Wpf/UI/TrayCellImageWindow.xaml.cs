using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WpfApp2.UI
{
    public partial class TrayCellImageWindow : Window
    {
        private TrayCellInfo _current;

        public TrayCellImageWindow()
        {
            InitializeComponent();
        }

        public TrayCellInfo Current => _current;

        public void UpdateInfo(TrayCellInfo info)
        {
            if (info == null)
            {
                return;
            }

            _current = info;
            PositionText.Text = string.Format(CultureInfo.InvariantCulture, "位置: ({0}, {1})", info.Row, info.Col);
            ResultText.Text = string.IsNullOrWhiteSpace(info.Result) ? "结果: -" : $"结果: {info.Result}";
            TimeText.Text = string.Format(CultureInfo.InvariantCulture, "时间: {0:yyyy-MM-dd HH:mm:ss}", info.DetectionTime);

            if (!string.IsNullOrWhiteSpace(info.ImagePath) && File.Exists(info.ImagePath))
            {
                CellImage.Source = LoadImage(info.ImagePath);
            }
            else
            {
                CellImage.Source = null;
            }
        }

        private static BitmapImage LoadImage(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
