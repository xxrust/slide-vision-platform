using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Collections.ObjectModel;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 展示最新检测数据下的2D-3D映射关系，无需手动输入
    /// </summary>
    public partial class ThreeDMappingAnalysisWindow : Window
    {
        private const double DefaultPixelSizeMm = 0.004;
        private const double Default3dMmMarginFactor = 1.2;
        private const double FrameWidthMm = 6.0;   // 3D灰度图物理宽度（mm）
        private const double FrameHeightMm = 2.8;  // 3D灰度图物理高度（mm）
        // 临时屏蔽灰度图显示（避免读取失败影响其他功能）
        private const bool EnableGrayImagePreview = false;
        private BitmapImage _grayBitmap = null;
        private double _frameWidthMm = FrameWidthMm;
        private double _frameHeightMm = FrameHeightMm;
        private readonly ObservableCollection<ChipCornerAnalysisItem> _cornerItems = new ObservableCollection<ChipCornerAnalysisItem>();
        private List<ChipCornerAnalysisItem> _cornerCache = new List<ChipCornerAnalysisItem>();
        private ChipHeightAnalysisParams3D _params3D = null;

        public ThreeDMappingAnalysisWindow(ChipHeightAnalysisSnapshot snapshot)
        {
            InitializeComponent();
            CornerListView.ItemsSource = _cornerItems;
            LoadSnapshot(snapshot);
        }

        private void LoadSnapshot(ChipHeightAnalysisSnapshot snapshot)
        {
            var safeSnapshot = snapshot ?? new ChipHeightAnalysisSnapshot
            {
                PixelSizeMm = DefaultPixelSizeMm
            };

            PixelSizeText.Text = $"像元尺寸: {safeSnapshot.PixelSizeMm * 1000:F2} μm/px";

            if (safeSnapshot.Has2D)
            {
                double chipLengthMm = safeSnapshot.Params2D.ChipLengthUm / 1000.0;
                double chipWidthMm = safeSnapshot.Params2D.ChipWidthUm / 1000.0;
                PkgCenter2DText.Text = $"PKG中心: ({safeSnapshot.Params2D.PkgCenterX:F2}, {safeSnapshot.Params2D.PkgCenterY:F2}) 像素";
                ChipCenterText.Text = $"晶片中心: ({safeSnapshot.Params2D.ChipCenterX:F2}, {safeSnapshot.Params2D.ChipCenterY:F2}) 像素";
                ChipSizeText.Text = $"晶片尺寸: {safeSnapshot.Params2D.ChipLengthUm:F2} × {safeSnapshot.Params2D.ChipWidthUm:F2} μm ({chipLengthMm:F4} × {chipWidthMm:F4} mm)";
                ChipAngleText.Text = $"晶片角度: {safeSnapshot.Params2D.ChipAngleDeg:F3}°";
            }
            else
            {
                PkgCenter2DText.Text = "PKG中心: --";
                ChipCenterText.Text = "晶片中心: --";
                ChipSizeText.Text = "晶片尺寸: --";
                ChipAngleText.Text = "晶片角度: --";
            }

            if (safeSnapshot.Has3D)
            {
                PkgCenter3DText.Text = $"PKG中心: ({safeSnapshot.Params3D.PkgCenterX:F4}, {safeSnapshot.Params3D.PkgCenterY:F4}) mm";
                PkgLineText.Text = $"PKG直线: Start=({safeSnapshot.Params3D.LineStartX:F4}, {safeSnapshot.Params3D.LineStartY:F4}) → End=({safeSnapshot.Params3D.LineEndX:F4}, {safeSnapshot.Params3D.LineEndY:F4})";
                PkgAngleText.Text = $"PKG角度: {safeSnapshot.Params3D.PkgAngleDeg:F3}°";
                ChipPlaneText.Text = $"晶片平面: Z = {safeSnapshot.Params3D.ChipPlaneA:F6} × X + {safeSnapshot.Params3D.ChipPlaneB:F6} × Y + {safeSnapshot.Params3D.ChipPlaneC:F4}";
                RefPlaneText.Text = $"参考平面: Z = {safeSnapshot.Params3D.RefPlaneA:F6} × X + {safeSnapshot.Params3D.RefPlaneB:F6} × Y + {safeSnapshot.Params3D.RefPlaneC:F4}";
            }
            else
            {
                PkgCenter3DText.Text = "PKG中心: --";
                PkgLineText.Text = "PKG直线: --";
                PkgAngleText.Text = "PKG角度: --";
                ChipPlaneText.Text = "晶片平面: --";
                RefPlaneText.Text = "参考平面: --";
            }

            if (safeSnapshot.HasCorners)
            {
                _cornerItems.Clear();
                foreach (var c in safeSnapshot.Corners.Where(c => c != null))
                {
                    _cornerItems.Add(c);
                }
                StatusText.Text = "已加载最新检测数据";
                _cornerCache = _cornerItems.ToList();
            }
            else
            {
                _cornerItems.Clear();
                StatusText.Text = "未找到有效的四角结果，请先完成一次3D+2D检测";
                _cornerCache = new List<ChipCornerAnalysisItem>();
            }

            _params3D = safeSnapshot.Params3D;
            if (EnableGrayImagePreview)
            {
                LoadGrayImage(safeSnapshot.GrayImagePath);
            }
            else
            {
                // 保持控件为空，避免触发灰度图加载/解析逻辑
                GrayImage.Source = null;
                GrayImageStatus.Text = "灰度图显示已屏蔽";
                CornerCanvas.Children.Clear();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var snapshot = Page1.PageManager.Page1Instance?.CreateChipHeightAnalysisSnapshot();
                LoadSnapshot(snapshot);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"刷新失败: {ex.Message}";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadGrayImage(string imagePath)
        {
            CornerCanvas.Children.Clear();
            _grayBitmap = null;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                GrayImage.Source = null;
                GrayImageStatus.Text = "未加载灰度图";
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                _grayBitmap = bitmap;
                GrayImage.Source = bitmap;
                GrayImageStatus.Text = $"灰度图: {Path.GetFileName(imagePath)}";

                UpdateCornerOverlay();
            }
            catch (Exception ex)
            {
                GrayImage.Source = null;
                GrayImageStatus.Text = $"加载灰度图失败: {ex.Message}";
            }
        }

        private void UpdateCornerOverlay()
        {
            CornerCanvas.Children.Clear();

            if (_grayBitmap == null || _cornerCache == null || _cornerCache.Count == 0)
            {
                return;
            }

            double containerWidth = GrayImage.ActualWidth;
            double containerHeight = GrayImage.ActualHeight;
            if (containerWidth < 1 || containerHeight < 1)
            {
                return;
            }

            double imgWidth = _grayBitmap.PixelWidth;
            double imgHeight = _grayBitmap.PixelHeight;
            double imgScale = Math.Min(containerWidth / imgWidth, containerHeight / imgHeight);
            if (imgScale <= 0) return;

            double drawWidth = imgWidth * imgScale;
            double drawHeight = imgHeight * imgScale;
            double offsetX = (containerWidth - drawWidth) / 2.0;
            double offsetY = (containerHeight - drawHeight) / 2.0;

            CornerCanvas.Width = containerWidth;
            CornerCanvas.Height = containerHeight;

            // 映射到灰度图：使用3D坐标，原点左上，无需镜像；frameW/frameH 为物理尺寸
            double frameW = _frameWidthMm;
            double frameH = _frameHeightMm;
            if (frameW <= 0) frameW = 6.0;
            if (frameH <= 0) frameH = 2.8;

            foreach (var corner in _cornerCache)
            {
                double x = offsetX + (corner.X3D / frameW) * drawWidth;
                double y = offsetY + (corner.Y3D / frameH) * drawHeight;

                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Stroke = Brushes.OrangeRed,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(90, 255, 140, 0))
                };
                Canvas.SetLeft(ellipse, x - ellipse.Width / 2);
                Canvas.SetTop(ellipse, y - ellipse.Height / 2);
                CornerCanvas.Children.Add(ellipse);

                var label = new TextBlock
                {
                    Text = corner.Name,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                    Padding = new Thickness(4, 2, 4, 2),
                    FontSize = 12
                };
                Canvas.SetLeft(label, x + 6);
                Canvas.SetTop(label, y - 6);
                CornerCanvas.Children.Add(label);
            }
        }

        private void GrayImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCornerOverlay();
        }
    }
}
