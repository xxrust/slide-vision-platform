using System;
using System.IO;
using System.Windows;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 文件选择窗口
    /// </summary>
    public partial class FileSelectionWindow : Window
    {
        private string _currentLotValue;
        
        public FileSelectionWindow(string lotValue)
        {
            InitializeComponent();
            _currentLotValue = lotValue;
        }

        /// <summary>
        /// 实时文档按钮点击事件 - 打开当前LOT的CSV文档文件夹
        /// </summary>
        private void RealTimeDocButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 构建实时文档路径：RealTimeData/LOT号/
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string realTimeDataDir = Path.Combine(baseDir, "RealTimeData", _currentLotValue);
                
                // 如果文件夹不存在，则创建
                if (!Directory.Exists(realTimeDataDir))
                {
                    Directory.CreateDirectory(realTimeDataDir);
                    LogManager.Info($"创建实时文档文件夹: {realTimeDataDir}");
                }
                
                // 使用资源管理器打开文件夹（正常窗口大小）
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = realTimeDataDir,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };
                System.Diagnostics.Process.Start(processStartInfo);
                LogManager.Info($"已打开实时文档文件夹: {realTimeDataDir}");
                
                this.Close(); // 关闭选择窗口
            }
            catch (Exception ex)
            {
                LogManager.Error($"打开实时文档文件夹失败: {ex.Message}");
                MessageBox.Show($"打开实时文档文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 图片按钮点击事件 - 打开当前LOT的图片文件夹（与图片检测功能使用相同目录）
        /// </summary>
        private void ImagesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用与图片检测功能完全相同的目录策略
                string currentSaveDir = GetCurrentImageSaveDirectory();
                //string ngDir = Path.Combine(currentSaveDir, "NG");

                // 如果NG目录存在，优先打开NG目录，否则打开当前存图目录
                string targetDir = currentSaveDir;

                // 如果存图目录也不存在，则创建它
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    LogManager.Info($"创建图像存储文件夹: {targetDir}");
                }

                // 使用资源管理器打开文件夹（正常窗口大小）
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = targetDir,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };
                System.Diagnostics.Process.Start(processStartInfo);
                LogManager.Info($"已打开图像存储文件夹: {targetDir}");
                
                this.Close(); // 关闭选择窗口
            }
            catch (Exception ex)
            {
                LogManager.Error($"打开图片文件夹失败: {ex.Message}");
                MessageBox.Show($"打开图片文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出的文档按钮点击事件 - 打开Export文件夹
        /// </summary>
        private void ExportDocButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 构建Export文件夹路径
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string exportDir = Path.Combine(baseDir, "Export");
                
                // 如果文件夹不存在，则创建
                if (!Directory.Exists(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                    LogManager.Info($"创建导出文件夹: {exportDir}");
                }
                
                // 使用资源管理器打开文件夹（正常窗口大小）
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = exportDir,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };
                System.Diagnostics.Process.Start(processStartInfo);
                LogManager.Info($"已打开导出文件夹: {exportDir}");
                
                this.Close(); // 关闭选择窗口
            }
            catch (Exception ex)
            {
                LogManager.Error($"打开导出文件夹失败: {ex.Message}");
                MessageBox.Show($"打开导出文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 查看渲染图按钮点击事件 - 打开渲染图查看器
        /// </summary>
        private void RenderViewerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("渲染图查看器已迁移为独立进程（Host/Tool）。\n当前版本主程序不再直接加载Keyence 3D渲染查看器。", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return;
            }
            catch (Exception ex)
            {
                LogManager.Error($"打开渲染图查看器失败: {ex.Message}");
                MessageBox.Show($"打开渲染图查看器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 获取当前图像保存目录
        /// </summary>
        private string GetCurrentImageSaveDirectory()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                //string todayFolder = DateTime.Now.ToString("yyyyMMdd");
                return Path.Combine(baseDir, "原图存储", _currentLotValue);
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取图像保存目录失败: {ex.Message}");
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }
    }
} 