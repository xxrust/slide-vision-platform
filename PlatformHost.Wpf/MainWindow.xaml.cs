using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VM.Core;
using VM.PlatformSDKCS;
using System.IO;
using static WpfApp2.UI.Page1;
using IMVSHPFeatureMatchModuCs;
using WpfApp2.UI;
using WpfApp2.Models;
using WpfApp2.UI.Models;
using Path = System.IO.Path;
using WpfApp2.ThreeD;

//using System.Windows.Forms;

namespace WpfApp2
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private VmProcedure procedure;
        private bool _closeConfirmationAccepted;

        //新建两个frame，使Page1.xaml和Page2.xaml可以在同一个窗口中切换
        public Frame frame1 = new Frame() { Content = new UI.Page1() };
        public Frame frame2 = new Frame() { Content = new UI.Page2() };
        //public Frame frame3 = new Frame() { Content = new UI.SearchPicture() };
        public Frame frame_ConfigPage = new Frame() { Content = new UI.ConfigPage() }; //配置页面
        public Frame frame_TemplateConfigPage; //模板配置 - 现在动态创建
        public Frame frame_CameraConfigPage = new Frame() { Content = new UI.CameraConfigPage() }; //相机配置页面
        // 🗑️ 已废弃：旧版数据分析页面，现已使用SmartAnalysisWindowManager替代
        // public Frame frame_DataAnalysisPage = new Frame() { Content = new UI.DataAnalysisPage() }; //数据分析页面
        public Frame frame_HardwareConfigPage = new Frame() { Content = new UI.HardwareConfigPage() }; //硬件配置页面

        public MainWindow()
        {
            InitializeComponent();
            ContentC.Content = frame1;

            // 使用Loaded事件确保UI完全初始化后再加载模板
            this.Loaded += (s, e) =>
            {
                // 延迟一小段时间，确保所有页面都已完全加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadLastUsedTemplate();
                    
                    // 初始化实时数据记录器
                    InitializeRealTimeDataLogger();
                    
                    // 🔧 新增：加载上次保存的生产统计数据
                    LoadProductionStatistics();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };

            this.Closing += MainWindow_Closing; // 注册关闭事件
        }

        /// <summary>
        /// 根据样品类型创建或更新模板配置页面
        /// </summary>
        /// <param name="sampleType">样品类型</param>
        /// <returns>模板配置页面实例</returns>
        public TemplateConfigPage CreateTemplateConfigPage(SampleType sampleType)
        {
            // 清理旧的模板配置页面实例（防止内存泄漏）
            CleanupOldTemplateConfigPage();

            var templateConfigPage = new TemplateConfigPage(sampleType);
            frame_TemplateConfigPage = new Frame() { Content = templateConfigPage };
            return templateConfigPage;
        }

        /// <summary>
        /// 根据样品类型和涂布类型创建或更新模板配置页面
        /// </summary>
        /// <param name="sampleType">样品类型</param>
        /// <param name="coatingType">涂布类型</param>
        /// <returns>模板配置页面实例</returns>
        public TemplateConfigPage CreateTemplateConfigPage(SampleType sampleType, CoatingType coatingType, string algorithmEngineId = null)
        {
            // 清理旧的模板配置页面实例（防止内存泄漏）
            CleanupOldTemplateConfigPage();

            var preferredEngineId = string.IsNullOrWhiteSpace(algorithmEngineId)
                ? AlgorithmEngineSettingsManager.PreferredEngineId
                : algorithmEngineId;
            var templateConfigPage = new TemplateConfigPage(sampleType, coatingType, preferredEngineId);
            frame_TemplateConfigPage = new Frame() { Content = templateConfigPage };
            return templateConfigPage;
        }

        /// <summary>
        /// 清理旧的模板配置页面实例，释放资源防止内存泄漏
        /// </summary>
        private void CleanupOldTemplateConfigPage()
        {
            try
            {
                if (frame_TemplateConfigPage?.Content is TemplateConfigPage oldPage)
                {
                    // 调用实例清理方法
                    oldPage.CleanupInstanceResources();

                    // 清除Frame的Content引用
                    frame_TemplateConfigPage.Content = null;

                    LogManager.Info("已清理旧的TemplateConfigPage实例");
                }

                // 强制进行垃圾回收（可选，但有助于立即释放大型资源）
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LogManager.Error($"清理旧TemplateConfigPage实例时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前的模板配置页面（如果存在）
        /// </summary>
        /// <returns>模板配置页面实例，如果不存在则返回null</returns>
        public TemplateConfigPage GetCurrentTemplateConfigPage()
        {
            return frame_TemplateConfigPage?.Content as TemplateConfigPage;
        }

        /// <summary>
        /// 初始化实时数据记录器
        /// </summary>
        private void InitializeRealTimeDataLogger()
        {
            try
            {
                // 初始化实时数据记录器（单例模式，会自动创建实例）
                var logger = WpfApp2.UI.Models.RealTimeDataLogger.Instance;
                
                // 从Page1获取当前LOT号并设置到记录器
                if (frame1?.Content is Page1 page1)
                {
                    string currentLot = page1.CurrentLotValue;
                    if (!string.IsNullOrEmpty(currentLot))
                    {
                        logger.SetLotNumber(currentLot);
                        LogManager.Info($"实时数据记录器已初始化，当前LOT号：{currentLot}");
                    }
                }
                
                LogManager.Info("实时数据记录器初始化完成");
                
                // 🔧 新增：在实时数据记录器初始化完成后，初始化系统检测管理器
                // 此时3D项目已经加载，可以正确判断3D启用状态
                try
                {
                    if (frame1?.Content is Page1 page1Instance)
                    {
                        LogManager.Info("[系统初始化] 开始初始化系统检测管理器（在实时数据记录器之后）");
                        page1Instance.InitializeDetectionManager();
                        LogManager.Info("[系统初始化] ✅ 系统检测管理器初始化完成");
                    }
                    else
                    {
                        LogManager.Error("[系统初始化] ❌ 无法获取Page1实例，系统检测管理器初始化失败");
                    }
                }
                catch (Exception managerEx)
                {
                    LogManager.Error($"[系统初始化] ❌ 系统检测管理器初始化失败: {managerEx.Message}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化实时数据记录器失败: {ex.Message}");
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_closeConfirmationAccepted)
            {
                var result = MessageBox.Show(
                    this,
                    "确认要关闭点胶检测系统吗？",
                    "退出确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }

                _closeConfirmationAccepted = true;
            }

            try
            {
                // 🔧 新增：保存当前生产统计数据
                WpfApp2.UI.ProductionStatsPersistence.SaveProductionStats();
                
                // 关闭实时数据记录器并保存所有数据
                WpfApp2.UI.Models.RealTimeDataLogger.Instance.Shutdown();
                LogManager.Info("实时数据记录器已关闭并保存数据");
            }
            catch (Exception ex)
            {
                LogManager.Error($"关闭实时数据记录器时出错: {ex.Message}");
            }
            // 3D Host/资源清理：主进程不再直接持有Keyence资源；如需停止Host，将在后续通过IPC处理。
            
            // 🔧 新增：释放串口资源，解决软件重启后连不上串口的问题
            try
            {
                // 先关闭所有可能正在使用串口的界面定时器
                StopAllSerialMonitoringTimers();
                
                // 再释放串口资源
                if (WpfApp2.SMTGPIO.PLCSerialController.Instance?.IsConnected == true)
                {
                    LogManager.Info("正在释放PLC串口资源...");
                    WpfApp2.SMTGPIO.PLCSerialController.Instance.Dispose();
                    LogManager.Info("PLC串口资源已释放");
                }
                else
                {
                    LogManager.Info("PLC串口未连接，无需释放");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"释放PLC串口资源时出错: {ex.Message}");
            }

            // 调用 CloseSolution 方法（已去除自动保存，改为手动保存）
            try
            {
                // ✅ 已移除自动保存：VmSolution.Save();
                VmSolution.Instance.Dispose();
                //不需要再调用 VmSolution.Instance.CloseSolution()，因为 Dispose 方法会自动处理关闭逻辑
                //VmSolution.Instance.CloseSolution();

            }
            catch (VmException ex)
            {
                MessageBox.Show(ex.Message.ToString());
            }
        }

        // 最后，在应用程序启动时自动加载最后使用的模板
        // 在MainWindow.xaml.cs的构造函数或Loaded事件中添加
        public void LoadLastUsedTemplate()
        {
            try
            {
                string configFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "LastUsedTemplate.txt");

                // 如果配置文件存在且不为空
                if (File.Exists(configFilePath))
                {
                    string templateFilePath = File.ReadAllText(configFilePath).Trim();

                    // 确保模板文件仍然存在
                    if (!string.IsNullOrEmpty(templateFilePath) && File.Exists(templateFilePath))
                    {
                        try
                        {
                            // 先读取模板以获取样品类型和涂布类型
                            var template = TemplateParameters.LoadFromFile(templateFilePath);

                            // 根据样品类型和涂布类型创建对应的模板配置页面
                            var templateConfigPage = CreateTemplateConfigPage(template.SampleType, template.CoatingType, template.AlgorithmEngineId);

                            // 自动加载模板（但不自动执行，避免重复执行）
                            templateConfigPage.LoadTemplate(templateFilePath, autoExecute: false);

                            // 同步模板中的相机参数到相机配置页面
                            try
                            {
                                if (frame_CameraConfigPage?.Content is CameraConfigPage cameraConfigPage)
                                {
                                    cameraConfigPage.LoadCameraParametersFromTemplate(templateFilePath);
                                    LogManager.Info($"已将模板中的相机参数加载到相机配置页: {Path.GetFileName(templateFilePath)}");
                                }
                            }
                            catch (Exception syncEx)
                            {
                                LogManager.Warning($"加载模板相机参数到配置页失败: {syncEx.Message}", "模板加载");
                            }

                            //将模板中的数值写入全局变量
                            templateConfigPage.ApplyParametersToGlobalVariables();
                            // 加载3D检测参数到主进程内存（与Keyence解耦）
                            ThreeDSettings.LoadFromTemplate(template);

                            // **新增：自动应用颜色配置到3D/2D视图**
                            ApplyColorConfigFromTemplate(template);

                            // **新增：自动执行模板**
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    templateConfigPage.AutoExecuteTemplate();
                                    LogManager.Info("已自动执行模板");
                                }
                                catch (Exception autoExecEx)
                                {
                                    LogManager.Error($"自动执行模板失败: {autoExecEx.Message}");
                                }
                            }), System.Windows.Threading.DispatcherPriority.Background);

                            // 获取样品类型信息用于显示
                            var sampleTypeInfo = TemplateParameters.GetAllSampleTypes()
                                .Find(s => s.Type == template.SampleType);

                            // 可选：自动切换到模板配置页面
                            //ContentC.Content = frame_TemplateConfigPage;

                            //MessageBox.Show($"已自动加载上次使用的模板:\n" +
                            //              $"模板: {template.TemplateName}\n" +
                            //              $"类型: {sampleTypeInfo?.DisplayName ?? "未知"}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"加载模板时出错: {ex.Message}");
                        }
                    }
                    else{
                        // 模板文件不存在时，创建一个默认的模板配置页面
                        CreateTemplateConfigPage(SampleType.Other);
                        LogManager.Info("模板文件不存在，已创建默认配置页面");
                    }
                }
                else
                {
                    // 配置文件不存在时，创建一个默认的模板配置页面
                    CreateTemplateConfigPage(SampleType.Other);
                    LogManager.Info("配置文件不存在，已创建默认配置页面");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"自动加载上次模板失败: {ex.Message}");
                // 出错时也创建一个默认的模板配置页面
                try
                {
                    CreateTemplateConfigPage(SampleType.Other);
                }
                catch (Exception createEx)
                {
                    MessageBox.Show($"创建默认配置页面失败: {createEx.Message}");
                }
            }
        }

        /// <summary>
        /// 停止所有串口监控定时器
        /// </summary>
        private void StopAllSerialMonitoringTimers()
        {
            try
            {
                LogManager.Info("正在停止所有串口监控定时器...");
                
                // 通知所有可能使用串口的页面停止定时器
                // 由于页面可能已经卸载，使用静态方法通知
                NotifyStopSerialMonitoring();
                
                LogManager.Info("串口监控定时器停止通知已发送");
            }
            catch (Exception ex)
            {
                LogManager.Error($"停止串口监控定时器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知停止串口监控（静态方法）
        /// </summary>
        private static void NotifyStopSerialMonitoring()
        {
            // 由于无法直接访问各页面实例，设置全局标志
            // 各页面的定时器应该检查此标志
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                // 如果当前有活动的串口相关窗口，直接关闭
                var allWindows = Application.Current.Windows.Cast<Window>().ToArray();
                foreach (var window in allWindows)
                {
                    try
                    {
                        // 检查是否是串口配置相关窗口
                        if (window.GetType().Name.Contains("PLC") || 
                            window.GetType().Name.Contains("Hardware") ||
                            window.GetType().Name.Contains("SystemTest"))
                        {
                            // 关闭前先停止定时器（如果窗口支持）
                            if (window is UI.SystemTestWindow systemTestWindow)
                            {
                                // SystemTestWindow有公共方法停止定时器的话在这里调用
                            }
                        }
                    }
                    catch
                    {
                        // 忽略关闭窗口时的异常
                    }
                }
            });
        }

        /// <summary>
        /// 从模板自动应用颜色配置到3D/2D视图
        /// </summary>
        private void ApplyColorConfigFromTemplate(TemplateParameters template)
        {
            try
            {
                if (template?.ColorParams == null)
                {
                    LogManager.Info("模板中无颜色配置，跳过自动应用");
                    return;
                }

                // 获取Page1实例
                var page1Instance = Page1.PageManager.Page1Instance;
                if (page1Instance == null)
                {
                    LogManager.Warning("Page1实例不存在，无法应用颜色配置");
                    return;
                }

                // 应用颜色配置到Page1的3D/2D视图
                page1Instance.ApplyColorConfigFromWindow(
                    template.ColorParams.UseCustomColorRange,
                    template.ColorParams.ColorRangeMin,
                    template.ColorParams.ColorRangeMax,
                    template.ColorParams.MeshTransparent,
                    template.ColorParams.BlendWeight,
                    template.ColorParams.DisplayColorBar,
                    template.ColorParams.DisplayGrid,
                    template.ColorParams.DisplayAxis
                );

                LogManager.Info($"已自动应用模板颜色配置: 自定义={template.ColorParams.UseCustomColorRange}, 范围=[{template.ColorParams.ColorRangeMin:F3}, {template.ColorParams.ColorRangeMax:F3}]");
            }
            catch (Exception ex)
            {
                LogManager.Error($"自动应用颜色配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载上次保存的生产统计数据
        /// </summary>
        private void LoadProductionStatistics()
        {
            try
            {
                // 延迟加载，确保TemplateConfigPage和StatisticsManager已经初始化
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        WpfApp2.UI.ProductionStatsPersistence.LoadProductionStats();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"延迟加载生产统计数据失败: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化生产统计数据加载失败: {ex.Message}");
            }
        }
    }
}
