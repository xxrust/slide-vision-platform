using System;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.Algorithms;
using static WpfApp2.UI.Page1;
using WpfApp2.Models;
using WpfApp2.UI.Models;
using WpfApp2.Hardware;
using System.IO;
using Microsoft.Win32;
using System.Windows.Forms;
using CSharp_OPTControllerAPI;
using WpfApp2.SMTGPIO;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WpfApp2.UI
{
    /// <summary>
    /// 相机参数配置页面
    /// </summary>
    public partial class CameraConfigPage : Page
    {
        private OPTControllerAPI optController; // 环形光源控制器 (192.168.1.16)
        private OPTControllerAPI coaxialOptController; // 同轴光源控制器 (192.168.1.20)
        private bool isLightControllerConnected = false; // 环形光源控制器连接状态
        private bool isCoaxialLightControllerConnected = false; // 同轴光源控制器连接状态
        private bool isMR13Active = false; // MR13置位状态
        private bool isInConfigMode = false; // 是否处于配置模式

        // SEQ表备份数据(192.168.1.20)
        private int backupSeqCount = 0;
        private int[] backupTriggerSource;
        private int[] backupIntensity;
        private int[] backupPulseWidth;

        // 45度和0度光使能状态
        private bool is45DegreeEnabled = false;
        private bool is0DegreeEnabled = false;


        // 图像选择状态 (1-飞拍, 2-定拍1, 3-定拍2)
        private int lidImageSelection = 2;     // LID图像默认选择定拍1
        private int coatingImageSelection = 3; // 镀膜图像默认选择定拍2

        /// <summary>
        /// 构造函数
        /// </summary>
        public CameraConfigPage()
        {
            InitializeComponent();
            optController = new OPTControllerAPI(); // 初始化环形光源控制器
            coaxialOptController = new OPTControllerAPI(); // 初始化同轴光源控制器

            ApplyCameraCatalogToUI();

            // 渲染控件绑定延后到页面加载完成后执行
            InitializeCameraParameters();
            InitializeLightController(); // 初始化环形光源控制器
            InitializeCoaxialLightController(); // 初始化同轴光源控制器
            LoadLastUsedCameraParameters(); // 加载最后使用的相机参数

            // 延迟绑定渲染控件，等待页面完全加载
            this.Loaded += CameraConfigPage_Loaded;

            LogMessage("相机配置页面初始化完成 - 配置模式需手动激活");
        }

        /// <summary>
        /// 页面加载完成事件处理器
        /// </summary>
        private void CameraConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                BindRenderControls();
                UpdateButtonStates();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ApplyCameraCatalogToUI()
        {
            try
            {
                if (FixedCamera1Title != null)
                {
                    FixedCamera1Title.Text = "图像源1";
                }

                if (FixedCamera2Title != null)
                {
                    FixedCamera2Title.Text = "图像源2";
                }

                if (FixedCamera1Panel != null)
                {
                    FixedCamera1Panel.Visibility = Visibility.Visible;
                }

                if (FixedCamera2Panel != null)
                {
                    FixedCamera2Panel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"应用相机配置到界面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 绑定渲染控件（平台不直接绑定算法模块）
        /// </summary>
        private void BindRenderControls()
        {
            LogMessage("渲染控件绑定已跳过（平台不直接绑定算法模块）");
        }

        /// <summary>
        /// 初始化光源控制器
        /// </summary>
        private void InitializeLightController()
        {
            try
            {
                // 尝试连接光源控制器
                string lightControllerIP = "192.168.1.16";
                int result = optController.CreateEthernetConnectionByIP(lightControllerIP);
                
                if (result == OPTControllerAPI.OPT_SUCCEED)
                {
                    isLightControllerConnected = true;
                    LogMessage($"光源控制器连接成功 - IP: {lightControllerIP}");
                    
                    // 读取当前SEQ表配置
                    ReadCurrentSeqTable();
                }
                else
                {
                    isLightControllerConnected = false;
                    LogMessage($"光源控制器连接失败 - IP: {lightControllerIP}, 错误代码: {result}");
                }
            }
            catch (Exception ex)
            {
                isLightControllerConnected = false;
                LogMessage($"初始化光源控制器时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化同轴光源控制器
        /// </summary>
        private void InitializeCoaxialLightController()
        {
            try
            {
                // 尝试连接同轴光源控制器
                string coaxialLightControllerIP = "192.168.1.20";
                int result = coaxialOptController.CreateEthernetConnectionByIP(coaxialLightControllerIP);

                if (result == OPTControllerAPI.OPT_SUCCEED)
                {
                    isCoaxialLightControllerConnected = true;
                    LogMessage($"同轴光源控制器连接成功 - IP: {coaxialLightControllerIP}");

                    // 读取当前同轴光SEQ表配置
                    ReadCurrentCoaxialSeqTable();
                }
                else
                {
                    isCoaxialLightControllerConnected = false;
                    LogMessage($"同轴光源控制器连接失败 - IP: {coaxialLightControllerIP}, 错误代码: {result}");
                }
            }
            catch (Exception ex)
            {
                isCoaxialLightControllerConnected = false;
                LogMessage($"初始化同轴光源控制器时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取当前SEQ表配置
        /// </summary>
        private void ReadCurrentSeqTable()
        {
            try
            {
                if (!isLightControllerConnected)
                {
                    LogMessage("⚠️ 光源控制器未连接，无法读取SEQ表");
                    return;
                }

                int moduleIndex = 1;
                int seqCount = 0;
                int[] triggerSource = new int[16]; // 预分配足够大的数组
                int[] intensity = new int[64];     // 预分配足够大的数组  
                int[] pulseWidth = new int[64];    // 预分配足够大的数组

                // 调用读取SEQ表API
                int result = optController.ReadSeqTable(moduleIndex, ref seqCount, triggerSource, intensity, pulseWidth);
                
                if (result == OPTControllerAPI.OPT_SUCCEED)
                {
                    LogMessage($"✅ SEQ表读取成功 - 模块{moduleIndex}:");
                    LogMessage($"  seqCount: {seqCount}");
                    
                    // 记录触发源
                    string triggerStr = "  triggerSource: [";
                    for (int i = 0; i < seqCount && i < triggerSource.Length; i++)
                    {
                        triggerStr += (i > 0 ? ", " : "") + triggerSource[i];
                    }
                    triggerStr += "]";
                    LogMessage(triggerStr);
                    
                    // 记录强度值
                    string intensityStr = "  intensity: [";
                    for (int i = 0; i < seqCount * 4 && i < intensity.Length; i++)
                    {
                        intensityStr += (i > 0 ? ", " : "") + intensity[i];
                    }
                    intensityStr += "]";
                    LogMessage(intensityStr);
                    
                    // 记录脉宽值
                    string pulseWidthStr = "  pulseWidth: [";
                    for (int i = 0; i < seqCount * 4 && i < pulseWidth.Length; i++)
                    {
                        pulseWidthStr += (i > 0 ? ", " : "") + pulseWidth[i];
                    }
                    pulseWidthStr += "]";
                    LogMessage(pulseWidthStr);
                }
                else
                {
                    LogMessage($"❌ SEQ表读取失败 - 错误代码: {result}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"读取SEQ表时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取当前同轴光SEQ表配置
        /// </summary>
        private void ReadCurrentCoaxialSeqTable()
        {
            try
            {
                if (!isCoaxialLightControllerConnected)
                {
                    LogMessage("⚠️ 同轴光源控制器未连接，无法读取SEQ表");
                    return;
                }

                int moduleIndex = 1;
                int seqCount = 0;
                int[] triggerSource = new int[16]; // 预分配足够大的数组
                int[] intensity = new int[64];     // 预分配足够大的数组
                int[] pulseWidth = new int[64];    // 预分配足够大的数组

                // 调用读取SEQ表API
                int result = coaxialOptController.ReadSeqTable(moduleIndex, ref seqCount, triggerSource, intensity, pulseWidth);

                if (result == OPTControllerAPI.OPT_SUCCEED)
                {
                    LogMessage($"✅ 同轴光SEQ表读取成功 - 模块{moduleIndex}:");
                    LogMessage($"  seqCount: {seqCount}");

                    // 记录触发源
                    string triggerStr = "  triggerSource: [";
                    for (int i = 0; i < seqCount && i < triggerSource.Length; i++)
                    {
                        triggerStr += (i > 0 ? ", " : "") + triggerSource[i];
                    }
                    triggerStr += "]";
                    LogMessage(triggerStr);

                    // 记录强度值（同轴光强度固定为255）
                    string intensityStr = "  intensity: [";
                    for (int i = 0; i < 8; i++)
                    {
                        intensityStr += (i > 0 ? ", " : "") + intensity[i];
                    }
                    intensityStr += "]";
                    LogMessage(intensityStr);

                    // 记录脉冲宽度（10us为单位）
                    string widthStr = "  pulseWidth(*10us): [";
                    for (int i = 0; i < 8; i++)
                    {
                        widthStr += (i > 0 ? ", " : "") + pulseWidth[i];
                    }
                    widthStr += "]";
                    LogMessage(widthStr);

                    // 如果SEQ表有数据，读取并显示当前的同轴光曝光时间
                    if (seqCount >= 2 && pulseWidth[0] > 0 && pulseWidth[4] > 0)
                    {
                        // 同轴光曝光时间：将1us单位转换为us
                        int coaxialTime1 = pulseWidth[0] * 1;
                        int coaxialTime2 = pulseWidth[4] * 1;
                        LogMessage($"  当前同轴光曝光时间: 步骤1={coaxialTime1}us, 步骤2={coaxialTime2}us");
                    }
                }
                else
                {
                    LogMessage($"❌ 同轴光SEQ表读取失败 - 错误代码: {result}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"读取同轴光SEQ表时出错: {ex.Message}");
            }
        }

        private void InitializeCameraParameters()
        {
            try
            {
                // 设置默认曝光时间（微秒）
                FlyingExposureSlider.Value = 8;
                FlyingExposureTextBox.Text = "8";
                
                // 设置默认延迟时间（微秒）
                FlyingDelaySlider.Value = 0;
                FlyingDelayTextBox.Text = "0";

                Fixed1ExposureTimeSlider.Value = 20;
                Fixed1ExposureTimeTextBox.Text = "20";

                Fixed2ExposureTimeSlider.Value = 20;
                Fixed2ExposureTimeTextBox.Text = "20";

                LogMessage("相机参数初始化完成");
            }
            catch (Exception ex)
            {
                LogMessage($"初始化相机参数失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 返回按钮点击事件
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否处于配置模式（MR13置位状态）
                if (isMR13Active && isInConfigMode)
                {
                    // 弹窗告警，禁止退出
                    var result = System.Windows.MessageBox.Show(
                        $"当前处于定拍测试模式中，请先停止测试后再退出配置界面。\n\n是否要先停止测试？",
                        "无法退出配置界面",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 用户选择停止测试，触发按钮点击事件
                        LightTestButton_Click(LightTestButton, new RoutedEventArgs());
                        return;
                    }
                    else
                    {
                        // 用户选择不停止测试，直接返回
                        LogMessage("⚠️ 用户取消退出操作，继续保持在配置模式");
                        return;
                    }
                }

                // 🔧 关键修复：从相机界面返回时重置检测管理器状态
                WpfApp2.UI.Page1.PageManager.ResetDetectionManagerOnPageReturn("相机配置页面");
                LogMessage("从相机界面返回：已重置检测状态并恢复检测处理");

                var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
                mainWindow.ContentC.Content = mainWindow.frame1; // 返回到主页面
                LogMessage("返回主界面");
            }
            catch (Exception ex)
            {
                LogMessage($"返回主界面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 定拍测试按钮点击事件
        /// </summary>
        private async void LightTestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 切换MR13状态
                if (isMR13Active)
                {
                    // 当前是置位状态，执行复位
                    bool resetSuccess = await PLCSerialController.Instance.ResetRelayAsync("MR13");
                    if (resetSuccess)
                    {
                        isMR13Active = false;
                        isInConfigMode = false;
                        LightTestButtonText.Text = "💡 定拍测试";
                        LightTestButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 99, 255)); // 恢复原色
                        LogMessage("✅ MR13复位成功，退出配置模式");

                        // 恢复SEQ表
                        RestoreOriginalSeqTable();

                        // 退出配置模式
                        ExitConfigurationMode();
                    }
                    else
                    {
                        LogMessage("❌ MR13复位失败");
                        System.Windows.MessageBox.Show("MR13复位失败，请检查PLC连接", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // 执行置位前先确认
                    var confirmResult = System.Windows.MessageBox.Show(
                        "⚠️ 即将进入定拍测试配置模式\n\n" +
                        "📍 当前状态：正常检测模式\n" +
                        "🔄 即将切换：配置调试模式\n" +
                        "❌ 切换后果：暂停所有检测处理和数据统计\n" +
                        "⚡ MR13状态：将被置位激活\n" +
                        "🔄 SEQ表修改：将重写192.168.1.20光源驱动器SEQ表\n" +
                        "🚪 退出限制：必须停止测试后才能退出界面\n\n" +
                        "💡 说明：配置模式期间系统不会进行任何产品判定和数据记录，\n" +
                        "     专用于设备调试和参数优化。\n\n" +
                        "是否确认进入配置模式？",
                        "进入配置模式确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);

                    if (confirmResult != MessageBoxResult.Yes)
                    {
                        LogMessage("用户取消进入配置模式");
                        return;
                    }

                    // 备份SEQ表并修改
                    if (!BackupAndModifySeqTable())
                    {
                        LogMessage("❌ SEQ表操作失败，取消进入测试模式");
                        System.Windows.MessageBox.Show("SEQ表操作失败，无法进入测试模式\n请检查192.168.1.20光源控制器连接", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 等待SEQ表写入生效（延迟500ms）
                    LogMessage("等待SEQ表写入生效...");
                    await System.Threading.Tasks.Task.Delay(500);

                    // SEQ表修改成功后执行置位
                    bool setSuccess = await PLCSerialController.Instance.SetRelayAsync("MR13");
                    if (setSuccess)
                    {
                        isMR13Active = true;
                        isInConfigMode = true;
                        LightTestButtonText.Text = "⚡ 停止测试";
                        LightTestButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 99, 99)); // 变为红色
                        LogMessage("✅ MR13置位成功，进入配置模式");

                        // 进入配置模式
                        TriggerConfigurationMode();
                    }
                    else
                    {
                        LogMessage("❌ MR13置位失败");
                        // 置位失败，恢复SEQ表
                        RestoreOriginalSeqTable();
                        System.Windows.MessageBox.Show("MR13置位失败，请检查PLC连接\nSEQ表已恢复", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"定拍测试操作失败: {ex.Message}");
                System.Windows.MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 备份并修改SEQ表以进入测试模式
        /// </summary>
        /// <returns>是否成功</returns>
        private bool BackupAndModifySeqTable()
        {
            try
            {
                if (!isCoaxialLightControllerConnected)
                {
                    LogMessage("⚠️ 同轴光源控制器（192.168.1.20）未连接");
                    return false;
                }

                // 备份当前SEQ表
                int moduleIndex = 1;
                backupSeqCount = 0;
                backupTriggerSource = new int[16];
                backupIntensity = new int[64];
                backupPulseWidth = new int[64];

                int result = coaxialOptController.ReadSeqTable(moduleIndex, ref backupSeqCount, backupTriggerSource, backupIntensity, backupPulseWidth);
                if (result != OPTControllerAPI.OPT_SUCCEED)
                {
                    LogMessage($"❌ 备份SEQ表失败 - 错误代码: {result}");
                    return false;
                }

                LogMessage($"✅ SEQ表备份成功 - 步骤数: {backupSeqCount}");
                LogMessage("正在修改SEQ表：移除第1步，保留第2、3步");

                // 修改SEQ表：只保留第2、第3步
                int newSeqCount = 2;
                int[] newTriggerSource = { 1, 1 }; // 外部触发
                int[] newIntensity = new int[8];
                int[] newPulseWidth = new int[8];

                // 如果原SEQ表有第2步，复制到新的第1步
                if (backupSeqCount >= 2)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        newIntensity[i] = backupIntensity[4 + i]; // 原第2步变为第1步
                        newPulseWidth[i] = backupPulseWidth[4 + i];
                    }
                }

                // 如果原SEQ表有第3步，复制到新的第2步
                if (backupSeqCount >= 3)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        newIntensity[4 + i] = backupIntensity[8 + i]; // 原第3步变为第2步
                        newPulseWidth[4 + i] = backupPulseWidth[8 + i];
                    }
                }

                // 写入修改后的SEQ表
                result = coaxialOptController.SetSeqTable(moduleIndex, newSeqCount, newTriggerSource, newIntensity, newPulseWidth);
                if (result != OPTControllerAPI.OPT_SUCCEED)
                {
                    LogMessage($"❌ 写入修改后的SEQ表失败 - 错误代码: {result}");
                    return false;
                }

                LogMessage("✅ SEQ表已成功修改为测试模式");

                // 验证SEQ表是否真的写入成功（可选：再读取一次确认）
                // 给光源控制器一点时间处理命令
                System.Threading.Thread.Sleep(100);

                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"备份和修改SEQ表时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复原SEQ表
        /// </summary>
        private void RestoreOriginalSeqTable()
        {
            try
            {
                if (!isCoaxialLightControllerConnected)
                {
                    LogMessage("⚠️ 同轴光源控制器（192.168.1.20）未连接，无法恢复SEQ表");
                    return;
                }

                if (backupSeqCount == 0)
                {
                    LogMessage("⚠️ 没有SEQ表备份数据，无需恢复");
                    return;
                }

                // 恢复原SEQ表
                int moduleIndex = 1;
                int result = coaxialOptController.SetSeqTable(moduleIndex, backupSeqCount, backupTriggerSource, backupIntensity, backupPulseWidth);

                if (result == OPTControllerAPI.OPT_SUCCEED)
                {
                    LogMessage("✅ SEQ表已成功恢复到原始状态");
                    // 仅记录日志，不弹窗通知

                    // 清空备份数据
                    backupSeqCount = 0;
                    backupTriggerSource = null;
                    backupIntensity = null;
                    backupPulseWidth = null;
                }
                else
                {
                    LogMessage($"❌ SEQ表恢复失败 - 错误代码: {result}");
                    System.Windows.MessageBox.Show(
                        $"❌ SEQ表恢复失败\n\n错误代码: {result}\n请手动检查192.168.1.20光源控制器设置",
                        "SEQ表恢复失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"恢复SEQ表时出错: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"❌ SEQ表恢复异常\n\n{ex.Message}\n请手动检查192.168.1.20光源控制器设置",
                    "SEQ表恢复异常",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 触发配置模式
        /// </summary>
        private void TriggerConfigurationMode()
        {
            try
            {
                // 进入配置模式，暂停检测处理
                LogMessage("🔧 已进入定拍测试配置模式");
                
                // 🔧 关键修复：通知检测管理器进入相机调试状态
                // 获取Page1实例的检测管理器并设置状态
                var page1Instance = WpfApp2.UI.Page1.PageManager.Page1Instance;
                if (page1Instance?.DetectionManager != null)
                {
                    page1Instance.DetectionManager.SetSystemState(SystemDetectionState.CameraAdjusting);
                    LogMessage("✅ 已通知检测管理器进入相机调试状态，暂停所有检测处理");
                }
                else
                {
                    LogMessage("⚠️ 无法访问检测管理器，配置模式可能无法完全生效");
                }

                // 仅在日志中记录，不弹窗
                LogMessage("配置模式已激活：可以开始进行定拍相机参数调试");
            }
            catch (Exception ex)
            {
                LogMessage($"触发配置模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 退出配置模式
        /// </summary>
        private void ExitConfigurationMode()
        {
            try
            {
                LogMessage("🔄 正在退出定拍测试配置模式");
                
                // 🔧 关键修复：通知检测管理器恢复正常检测状态
                // 获取Page1实例的检测管理器并恢复状态
                var page1Instance = WpfApp2.UI.Page1.PageManager.Page1Instance;
                if (page1Instance?.DetectionManager != null)
                {
                    page1Instance.DetectionManager.SetSystemState(SystemDetectionState.WaitingForTrigger);
                    LogMessage("✅ 已通知检测管理器恢复正常检测状态，重新启用检测处理");
                }
                else
                {
                    LogMessage("⚠️ 无法访问检测管理器，状态恢复可能不完整");
                }

                // 仅在日志中记录退出状态，不弹窗
                LogMessage("已成功退出定拍测试配置模式，系统恢复正常检测");
            }
            catch (Exception ex)
            {
                LogMessage($"退出配置模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否处于配置模式
        /// </summary>
        /// <returns>是否处于配置模式</returns>
        public bool IsInConfigMode()
        {
            return isMR13Active && isInConfigMode;
        }

        /// <summary>
        /// 保存图片按钮点击事件
        /// </summary>
        private void SaveImagesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取默认保存路径
                string defaultSavePath = GetDefaultImageSavePath();
                
                // 显示保存文件对话框
                Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "选择图片保存位置和文件名",
                    Filter = "图片文件|*.bmp;*.jpg;*.png|BMP文件|*.bmp|JPEG文件|*.jpg|PNG文件|*.png",
                    DefaultExt = "bmp",
                    FileName = $"相机图片_{DateTime.Now:yyyyMMdd_HHmmss}",
                    InitialDirectory = defaultSavePath
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string baseFileName = Path.GetFileNameWithoutExtension(saveDialog.FileName);
                    string directory = Path.GetDirectoryName(saveDialog.FileName);
                    string extension = Path.GetExtension(saveDialog.FileName);
                    
                    // 禁用保存按钮，防止重复点击
                    SaveImagesButton.IsEnabled = false;
                    SaveImagesButton.Content = "正在保存...";
                    
                    try
                    {
                        SaveThreeImages(directory, baseFileName, extension);
                    }
                    finally
                    {
                        // 恢复按钮状态
                        SaveImagesButton.IsEnabled = true;
                        SaveImagesButton.Content = new StackPanel
                        {
                            Orientation = System.Windows.Controls.Orientation.Horizontal,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children = { new TextBlock { Text = "📷 保存图片", Foreground = System.Windows.Media.Brushes.White, FontSize = 16, VerticalAlignment = VerticalAlignment.Center } }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"保存图片失败: {ex.Message}";
                LogMessage(errorMsg);
                System.Windows.MessageBox.Show(errorMsg, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 确保按钮状态正确恢复
                SaveImagesButton.IsEnabled = true;
                SaveImagesButton.Content = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { new TextBlock { Text = "📷 保存图片", Foreground = System.Windows.Media.Brushes.White, FontSize = 16, VerticalAlignment = VerticalAlignment.Center } }
                };
            }
        }

        /// <summary>
        /// 获取默认图片保存路径
        /// </summary>
        /// <returns>默认保存路径</returns>
        private string GetDefaultImageSavePath()
        {
            try
            {
                // 在程序目录下创建Images文件夹，按日期分类
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string imagesDir = Path.Combine(baseDir, "Images");
                string dateDir = Path.Combine(imagesDir, DateTime.Now.ToString("yyyy-MM-dd"));
                
                // 确保目录存在
                if (!Directory.Exists(dateDir))
                {
                    Directory.CreateDirectory(dateDir);
                }
                
                return dateDir;
            }
            catch (Exception ex)
            {
                LogMessage($"创建默认保存路径失败: {ex.Message}");
                return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
        }

        /// <summary>
        /// 同时保存三张相机图片
        /// </summary>
        /// <param name="directory">保存目录</param>
        /// <param name="baseFileName">基础文件名</param>
        /// <param name="extension">文件扩展名</param>
        private void SaveThreeImages(string directory, string baseFileName, string extension)
        {
            try
            {
                int successCount = 0;
                string errorMessages = "";

                // 保存飞拍相机图片 (xxx-1)
                try
                {
                    string flyingImagePath = Path.Combine(directory, $"{baseFileName}-1{extension}");
                    FlyingCameraRender.SaveOriginalImage(flyingImagePath);
                    successCount++;
                    LogMessage($"飞拍相机图片已保存: {flyingImagePath}");
                }
                catch (Exception ex)
                {
                    errorMessages += $"飞拍相机图片保存失败: {ex.Message}\n";
                }

                // 保存定拍相机1图片 (xxx-2)
                try
                {
                    string fixed1ImagePath = Path.Combine(directory, $"{baseFileName}-2{extension}");
                    FixedCamera1Render.SaveOriginalImage(fixed1ImagePath);
                    successCount++;
                    LogMessage($"定拍相机1图片已保存: {fixed1ImagePath}");
                }
                catch (Exception ex)
                {
                    errorMessages += $"定拍相机1图片保存失败: {ex.Message}\n";
                }

                // 保存定拍相机2图片 (xxx-3)
                try
                {
                    string fixed2ImagePath = Path.Combine(directory, $"{baseFileName}-3{extension}");
                    FixedCamera2Render.SaveOriginalImage(fixed2ImagePath);
                    successCount++;
                    LogMessage($"定拍相机2图片已保存: {fixed2ImagePath}");
                }
                catch (Exception ex)
                {
                    errorMessages += $"定拍相机2图片保存失败: {ex.Message}\n";
                }

                // 显示保存结果
                if (successCount == 3)
                {
                    System.Windows.MessageBox.Show($"所有图片保存成功！\n保存位置: {directory}\n文件名: {baseFileName}-1, {baseFileName}-2, {baseFileName}-3", 
                        "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (successCount > 0)
                {
                    System.Windows.MessageBox.Show($"部分图片保存成功 ({successCount}/3)\n\n错误信息:\n{errorMessages}", 
                        "部分保存成功", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    System.Windows.MessageBox.Show($"所有图片保存失败\n\n错误信息:\n{errorMessages}", 
                        "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"保存图片过程中发生错误: {ex.Message}";
                LogMessage(errorMsg);
                System.Windows.MessageBox.Show(errorMsg, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 飞拍相机控制

        /// <summary>
        /// 飞拍相机曝光时间滑块变化事件
        /// </summary>
        private void FlyingExposureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FlyingExposureTextBox != null)
            {
                FlyingExposureTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        /// <summary>
        /// 飞拍相机曝光时间文本框变化事件
        /// </summary>
        private void FlyingExposureTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(FlyingExposureTextBox.Text, out int value))
            {
                if (value >= FlyingExposureSlider.Minimum && value <= FlyingExposureSlider.Maximum)
                {
                    FlyingExposureSlider.Value = value;
                }
            }
        }

        /// <summary>
        /// 飞拍相机延迟时间滑块变化事件
        /// </summary>
        private void FlyingDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FlyingDelayTextBox != null)
            {
                FlyingDelayTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        /// <summary>
        /// 飞拍相机延迟时间文本框变化事件
        /// </summary>
        private void FlyingDelayTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(FlyingDelayTextBox.Text, out int value))
            {
                if (value >= FlyingDelaySlider.Minimum && value <= FlyingDelaySlider.Maximum)
                {
                    FlyingDelaySlider.Value = value;
                }
            }
        }

        /// <summary>
        /// 飞拍相机应用参数按钮点击事件
        /// </summary>
        private void FlyingApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有测试按钮处于激活状态
                if (isMR13Active)
                {
                    System.Windows.MessageBox.Show("当前处于定拍测试模式中，请先停止测试后再应用参数。",
                        "无法应用参数", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LogMessage("⚠️ 应用参数被阻止 - 当前处于定拍测试模式");
                    return;
                }

                // 获取曝光时间值（整数）
                if (!int.TryParse(FlyingExposureTextBox.Text, out int exposureTime))
                {
                    System.Windows.MessageBox.Show("请输入有效的整数曝光时间值", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 获取延迟时间值（整数）
                if (!int.TryParse(FlyingDelayTextBox.Text, out int delayTime))
                {
                    System.Windows.MessageBox.Show("请输入有效的整数延迟时间值", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 设置飞拍曝光时间全局变量（算法层解耦）
                AlgorithmGlobalVariables.Set("飞拍曝光时间", exposureTime.ToString());
                
                // 设置飞拍延迟时间全局变量（算法层解耦）
                AlgorithmGlobalVariables.Set("延迟时间_us", delayTime.ToString());
                
                // **新增：将延迟时间写入PLC的DM0.0**
                WriteFlyingDelayToPLC(delayTime);
                
                // 自动保存相机参数到模板
                AutoSaveCameraParameters();
                
                LogMessage($"已设置飞拍相机参数 - 曝光时间: {exposureTime}us, 延迟时间: {delayTime}us");
                System.Windows.MessageBox.Show($"飞拍相机参数已应用并保存到模板\n曝光时间: {exposureTime}us\n延迟时间: {delayTime}us\n\n延迟时间已同步写入PLC DM0.0", "参数应用成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string errorMsg = $"应用飞拍相机参数失败: {ex.Message}";
                LogMessage(errorMsg);
                System.Windows.MessageBox.Show(errorMsg, "应用失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 将飞拍延迟时间写入PLC的DM0（异步版本，避免界面卡死）
        /// </summary>
        /// <param name="delayValue">延迟时间值（微秒）</param>
        private void WriteFlyingDelayToPLC(int delayValue)
        {
            try
            {
                // 使用PLC控制器单例实例
                var plcController = WpfApp2.SMTGPIO.PLCSerialController.Instance;
                
                if (plcController == null || !plcController.IsConnected)
                {
                    LogMessage("⚠️ PLC未连接，跳过延迟时间写入");
                    return;
                }

                // 使用UI线程安全的异步方法写入DM0，避免界面卡死
                plcController.WriteSingleUIAsync("DM0", delayValue, 
                    onSuccess: (success) =>
                    {
                        if (success)
                        {
                            LogMessage($"✅ 飞拍延迟时间已写入PLC DM0: {delayValue}us");
                        }
                        else
                        {
                            LogMessage($"❌ 写入PLC DM0失败: {plcController.ErrorMessage}");
                        }
                    },
                    onError: (error) =>
                    {
                        LogMessage($"❌ 写入PLC延迟时间时出错: {error}");
                    });
            }
            catch (Exception ex)
            {
                LogMessage($"写入PLC延迟时间时出错: {ex.Message}");
            }
        }

        #endregion

        #region 45度和0度光使能控制

        /// <summary>
        /// 45度光使能复选框选中事件
        /// </summary>
        private void Enable45DegreeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            is45DegreeEnabled = true;
            UpdateCoaxialLightSEQTable();
            LogMessage("45度光已启用");
        }

        /// <summary>
        /// 45度光使能复选框取消选中事件
        /// </summary>
        private void Enable45DegreeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            is45DegreeEnabled = false;
            UpdateCoaxialLightSEQTable();
            LogMessage("45度光已禁用");
        }

        /// <summary>
        /// 0度光使能复选框选中事件
        /// </summary>
        private void Enable0DegreeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            is0DegreeEnabled = true;
            UpdateCoaxialLightSEQTable();
            LogMessage("0度光已启用");
        }

        /// <summary>
        /// 0度光使能复选框取消选中事件
        /// </summary>
        private void Enable0DegreeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            is0DegreeEnabled = false;
            UpdateCoaxialLightSEQTable();
            LogMessage("0度光已禁用");
        }

        /// <summary>
        /// 更新同轴光控制器的SEQ表（包括45度、0度和同轴光）
        /// </summary>
        private void UpdateCoaxialLightSEQTable()
        {
            try
            {
                if (!isCoaxialLightControllerConnected)
                {
                    LogMessage("⚠️ 同轴光源控制器（192.168.1.20）未连接");
                    return;
                }

                // 获取定拍同轴光曝光时间（默认为0）
                int coaxialTime1 = int.TryParse(Fixed1CoaxialTimeTextBox?.Text, out int ct1) ? ct1 : 0;
                int coaxialTime2 = int.TryParse(Fixed2CoaxialTimeTextBox?.Text, out int ct2) ? ct2 : 0;

                // 配置3步SEQ表
                // 步骤1：飞拍触发的45度/0度光（根据使能状态）
                // 步骤2：定拍1的同轴光（CH3）
                // 步骤3：定拍2的同轴光（CH3）
                int moduleIndex = 1;
                int seqCount = 3;
                int[] triggerSource = { 1, 1, 1 }; // 都使用外部触发

                // 初始化强度数组（3步 * 4通道）
                int[] intensity = new int[12];
                int[] pulseWidth = new int[12];

                //高亮光源驱动器的单位为1us，不是10us
                // 步骤1（位置0-3）：飞拍的45度和0度光
                // CH1(位置0): 0度光
                intensity[0] = is0DegreeEnabled ? 255 : 0;
                pulseWidth[0] = is0DegreeEnabled ? 100 : 0; // 100us (单位是1us)

                // CH2(位置1): 45度光
                intensity[1] = is45DegreeEnabled ? 255 : 0;
                pulseWidth[1] = is45DegreeEnabled ? 100 : 0; // 100us (单位是1us)

                // CH3和CH4(位置2-3): 不使用
                intensity[2] = 0;
                intensity[3] = 0;
                pulseWidth[2] = 0;
                pulseWidth[3] = 0;

                // 步骤2（位置4-7）：定拍1的同轴光
                intensity[4] = 0;  // CH1: 0度光不使用
                intensity[5] = 0;  // CH2: 45度光不使用
                intensity[6] = 255; // CH3: 同轴光，强度255
                intensity[7] = 0;  // CH4: 不使用

                pulseWidth[4] = 0;
                pulseWidth[5] = 0;
                pulseWidth[6] = coaxialTime1; // 同轴光曝光时间，直接使用us值（单位是1us）
                pulseWidth[7] = 0;

                // 步骤3（位置8-11）：定拍2的同轴光
                intensity[8] = 0;  // CH1: 0度光不使用
                intensity[9] = 0;  // CH2: 45度光不使用
                intensity[10] = 255; // CH3: 同轴光，强度255
                intensity[11] = 0;  // CH4: 不使用

                pulseWidth[8] = 0;
                pulseWidth[9] = 0;
                pulseWidth[10] = coaxialTime2; // 同轴光曝光时间，直接使用us值（单位是1us）
                pulseWidth[11] = 0;

                LogMessage($"准备写入同轴光控制器SEQ表（192.168.1.20）：");
                LogMessage($"  步骤1 - 飞拍: 0度光={is0DegreeEnabled}, 45度光={is45DegreeEnabled}");
                LogMessage($"  步骤2 - 定拍1同轴光: {coaxialTime1}us");
                LogMessage($"  步骤3 - 定拍2同轴光: {coaxialTime2}us");

                // 写入SEQ表
                int result = coaxialOptController.SetSeqTable(moduleIndex, seqCount, triggerSource, intensity, pulseWidth);

                if (result == OPTControllerAPI.OPT_SUCCEED)
                {
                    LogMessage("✅ 同轴光控制器SEQ表写入成功");
                }
                else
                {
                    LogMessage($"❌ 同轴光控制器SEQ表写入失败 - 错误代码: {result}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"更新同轴光SEQ表时出错: {ex.Message}");
            }
        }

        #endregion

        #region 定拍相机1控制

        /// <summary>
        /// 定拍相机1应用参数按钮点击事件
        /// </summary>
        private void Fixed1ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有测试按钮处于激活状态
                if (isMR13Active)
                {
                    System.Windows.MessageBox.Show("当前处于定拍测试模式中，请先停止测试后再应用参数。",
                        "无法应用参数", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LogMessage("⚠️ 应用参数被阻止 - 当前处于定拍测试模式");
                    return;
                }

                // 获取曝光时间值（整数）
                if (!int.TryParse(Fixed1ExposureTimeTextBox.Text, out int exposureTimeValue))
                {
                    System.Windows.MessageBox.Show("请输入有效的整数曝光时间值", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证曝光时间值范围 (0-1000us)
                if (exposureTimeValue < 0 || exposureTimeValue > 1000)
                {
                    System.Windows.MessageBox.Show("曝光时间值必须在0-1000us范围内", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 获取同轴光曝光时间值
                int coaxialTime1 = 100; // 默认值
                if (int.TryParse(Fixed1CoaxialTimeTextBox?.Text, out int parsedCoaxialTime1))
                {
                    coaxialTime1 = parsedCoaxialTime1;
                }

                int coaxialTime2 = 100; // 默认值
                if (int.TryParse(Fixed2CoaxialTimeTextBox?.Text, out int parsedCoaxialTime2))
                {
                    coaxialTime2 = parsedCoaxialTime2;
                }

                // 写入光源驱动器（包括环形光和同轴光），强度固定为255
                bool lightResult = WriteLightIntensityAndTimeToController(255, exposureTimeValue, 255,
                    int.TryParse(Fixed2ExposureTimeTextBox.Text, out int time2) ? time2 : 100,
                    coaxialTime1, coaxialTime2);

                // 自动保存相机参数到模板
                AutoSaveCameraParameters();

                string message = $"定拍相机1参数已应用并保存到模板\n环形光强度: 255（固定值）\n环形光曝光时间: {exposureTimeValue}us\n同轴光曝光时间: {coaxialTime1}us";
                if (lightResult)
                {
                    message += "\n✅ 环形光和同轴光参数已成功写入控制器";
                }
                else
                {
                    message += "\n⚠️ 光源参数写入失败或控制器未连接";
                }

                LogMessage($"已设置定拍相机1参数 - 曝光强度: 255（固定）, 曝光时间: {exposureTimeValue}us");
                System.Windows.MessageBox.Show(message, "参数应用成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string errorMsg = $"应用定拍相机1参数失败: {ex.Message}";
                LogMessage(errorMsg);
                System.Windows.MessageBox.Show(errorMsg, "应用失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 定拍相机1曝光时间滑块变化事件
        /// </summary>
        private void Fixed1ExposureTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Fixed1ExposureTimeTextBox != null)
            {
                Fixed1ExposureTimeTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        /// <summary>
        /// 定拍相机1曝光时间文本框变化事件
        /// </summary>
        private void Fixed1ExposureTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Fixed1ExposureTimeTextBox.Text, out int value))
            {
                // 确保值是10的倍数（因为SEQ表单位是10us）
                value = (value / 10) * 10;
                if (value >= Fixed1ExposureTimeSlider.Minimum && value <= Fixed1ExposureTimeSlider.Maximum)
                {
                    Fixed1ExposureTimeSlider.Value = value;
                    // 如果文本框的值不是10的倍数，更新为最接近的10的倍数
                    if (Fixed1ExposureTimeTextBox.Text != value.ToString())
                    {
                        Fixed1ExposureTimeTextBox.Text = value.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// 定拍相机1同轴光曝光时间滑块变化事件
        /// </summary>
        private void Fixed1CoaxialTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Fixed1CoaxialTimeTextBox != null)
            {
                Fixed1CoaxialTimeTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        /// <summary>
        /// 定拍相机1同轴光曝光时间文本框变化事件
        /// </summary>
        private void Fixed1CoaxialTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Fixed1CoaxialTimeTextBox.Text, out int value))
            {
                // 确保值是10的倍数（因为SEQ表单位是10us）
                value = (value / 10) * 10;
                if (value >= Fixed1CoaxialTimeSlider.Minimum && value <= Fixed1CoaxialTimeSlider.Maximum)
                {
                    Fixed1CoaxialTimeSlider.Value = value;
                    // 如果文本框的值不是10的倍数，更新为最接近的10的倍数
                    if (Fixed1CoaxialTimeTextBox.Text != value.ToString())
                    {
                        Fixed1CoaxialTimeTextBox.Text = value.ToString();
                    }
                }
            }
        }

        #endregion

        #region 定拍相机2控制

        /// <summary>
        /// 定拍相机2应用参数按钮点击事件
        /// </summary>
        private void Fixed2ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有测试按钮处于激活状态
                if (isMR13Active)
                {
                    System.Windows.MessageBox.Show("当前处于定拍测试模式中，请先停止测试后再应用参数。",
                        "无法应用参数", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LogMessage("⚠️ 应用参数被阻止 - 当前处于定拍测试模式");
                    return;
                }

                // 获取曝光时间值（整数）
                if (!int.TryParse(Fixed2ExposureTimeTextBox.Text, out int exposureTimeValue))
                {
                    System.Windows.MessageBox.Show("请输入有效的整数曝光时间值", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证曝光时间值范围 (0-1000us)
                if (exposureTimeValue < 0 || exposureTimeValue > 1000)
                {
                    System.Windows.MessageBox.Show("曝光时间值必须在0-1000us范围内", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 获取同轴光曝光时间值
                int coaxialTime1 = 100; // 默认值
                if (int.TryParse(Fixed1CoaxialTimeTextBox?.Text, out int parsedCoaxialTime1))
                {
                    coaxialTime1 = parsedCoaxialTime1;
                }

                int coaxialTime2 = 100; // 默认值
                if (int.TryParse(Fixed2CoaxialTimeTextBox?.Text, out int parsedCoaxialTime2))
                {
                    coaxialTime2 = parsedCoaxialTime2;
                }

                // 写入光源驱动器（包括环形光和同轴光），强度固定为255
                bool lightResult = WriteLightIntensityAndTimeToController(255,
                    int.TryParse(Fixed1ExposureTimeTextBox.Text, out int time1) ? time1 : 100, 255, exposureTimeValue,
                    coaxialTime1, coaxialTime2);

                // 自动保存相机参数到模板
                AutoSaveCameraParameters();

                string message = $"定拍相机2参数已应用并保存到模板\n环形光强度: 255（固定值）\n环形光曝光时间: {exposureTimeValue}us\n同轴光曝光时间: {coaxialTime2}us";
                if (lightResult)
                {
                    message += "\n✅ 环形光和同轴光参数已成功写入控制器";
                }
                else
                {
                    message += "\n⚠️ 光源参数写入失败或控制器未连接";
                }

                LogMessage($"已设置定拍相机2参数 - 曝光强度: 255（固定）, 曝光时间: {exposureTimeValue}us");
                System.Windows.MessageBox.Show(message, "参数应用成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string errorMsg = $"应用定拍相机2参数失败: {ex.Message}";
                LogMessage(errorMsg);
                System.Windows.MessageBox.Show(errorMsg, "应用失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 定拍相机2曝光时间滑块变化事件
        /// </summary>
        private void Fixed2ExposureTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Fixed2ExposureTimeTextBox != null)
            {
                Fixed2ExposureTimeTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        /// <summary>
        /// 定拍相机2曝光时间文本框变化事件
        /// </summary>
        private void Fixed2ExposureTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Fixed2ExposureTimeTextBox.Text, out int value))
            {
                // 确保值是10的倍数（因为SEQ表单位是10us）
                value = (value / 10) * 10;
                if (value >= Fixed2ExposureTimeSlider.Minimum && value <= Fixed2ExposureTimeSlider.Maximum)
                {
                    Fixed2ExposureTimeSlider.Value = value;
                    // 如果文本框的值不是10的倍数，更新为最接近的10的倍数
                    if (Fixed2ExposureTimeTextBox.Text != value.ToString())
                    {
                        Fixed2ExposureTimeTextBox.Text = value.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// 定拍相机2同轴光曝光时间滑块变化事件
        /// </summary>
        private void Fixed2CoaxialTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Fixed2CoaxialTimeTextBox != null)
            {
                Fixed2CoaxialTimeTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        /// <summary>
        /// 定拍相机2同轴光曝光时间文本框变化事件
        /// </summary>
        private void Fixed2CoaxialTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Fixed2CoaxialTimeTextBox.Text, out int value))
            {
                // 确保值是10的倍数（因为SEQ表单位是10us）
                value = (value / 10) * 10;
                if (value >= Fixed2CoaxialTimeSlider.Minimum && value <= Fixed2CoaxialTimeSlider.Maximum)
                {
                    Fixed2CoaxialTimeSlider.Value = value;
                    // 如果文本框的值不是10的倍数，更新为最接近的10的倍数
                    if (Fixed2CoaxialTimeTextBox.Text != value.ToString())
                    {
                        Fixed2CoaxialTimeTextBox.Text = value.ToString();
                    }
                }
            }
        }

        #endregion

        #region 辅助方法

        private bool ShouldSkipLightControllerIo()
        {
            try
            {
                var page1 = WpfApp2.UI.Page1.PageManager.Page1Instance;
                if (page1 == null)
                {
                    return true;
                }

                // 图片检测模式：不应触发任何光源驱动器/SEQ操作
                if (page1.IsInImageTestMode())
                {
                    return true;
                }

                // 模板配置/校准(相机调试)等模式：不应触发光源驱动器/SEQ操作
                var manager = page1.DetectionManager;
                if (manager == null)
                {
                    return true;
                }

                var state = manager.SystemState;
                return state == SystemDetectionState.TemplateConfiguring
                       || state == SystemDetectionState.CameraAdjusting
                       || state == SystemDetectionState.Maintenance;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 将光源强度和曝光时间写入光源控制器
        /// </summary>
        /// <param name="intensity1">定拍1环形光源强度</param>
        /// <param name="exposureTime1">定拍1环形光曝光时间(us)</param>
        /// <param name="intensity2">定拍2环形光源强度</param>
        /// <param name="exposureTime2">定拍2环形光曝光时间(us)</param>
        /// <param name="coaxialTime1">定拍1同轴光曝光时间(us)</param>
        /// <param name="coaxialTime2">定拍2同轴光曝光时间(us)</param>
        /// <returns>是否写入成功</returns>
        private bool WriteLightIntensityAndTimeToController(int intensity1, int exposureTime1, int intensity2, int exposureTime2,
            int coaxialTime1 = 100, int coaxialTime2 = 100)
        {
            bool ringLightResult = false;
            bool coaxialLightResult = false;

            // 写入环形光参数到192.168.1.16
            try
            {
                if (!isLightControllerConnected)
                {
                    LogMessage("⚠️ 环形光源控制器未连接，跳过环形光强度和曝光时间写入");
                }
                else
                {
                    // 验证强度值必须为255
                    if (intensity1 != 255 || intensity2 != 255)
                    {
                        LogMessage($"❌ 环形光强度值必须为255: 定拍1={intensity1}, 定拍2={intensity2}");
                    }
                    else if (exposureTime1 < 0 || exposureTime1 > 1000 || exposureTime2 < 0 || exposureTime2 > 1000)
                    {
                        LogMessage($"❌ 环形光曝光时间超出范围(0-1000us): 定拍1={exposureTime1}us, 定拍2={exposureTime2}us");
                    }
                    else
                    {
                        // 使用SetSeqTable函数，根据读取到的SEQ表格式设置参数
                        int moduleIndex = 1;
                        int seqCount = 2;
                        int[] triggerSource = { 1, 1 };
                        // 按照读取到的格式：步骤1在[0]位置，步骤2在[4]位置
                        int[] intensity = { intensity1, 0, 0, 0, intensity2, 0, 0, 0 };
                        // 曝光时间需要转换：输入单位是us，SEQ表单位是10us，所以需要除以10
                        int pulseWidth1 = exposureTime1 / 10;  // 将us转换为10us单位
                        int pulseWidth2 = exposureTime2 / 10;  // 将us转换为10us单位
                        int[] pulseWidth = { pulseWidth1, 0, 0, 0, pulseWidth2, 0, 0, 0 };

                        LogMessage($"准备写入环形光SEQ表 - 步骤1强度:{intensity1}/时间:{exposureTime1}us({pulseWidth1}*10us), 步骤2强度:{intensity2}/时间:{exposureTime2}us({pulseWidth2}*10us)");

                        // 调用光源控制器API写入SEQ表
                        int result = optController.SetSeqTable(moduleIndex, seqCount, triggerSource, intensity, pulseWidth);

                        if (result == OPTControllerAPI.OPT_SUCCEED)
                        {
                            LogMessage($"✅ 环形光强度和曝光时间写入成功 - 步骤1: {intensity1}/{exposureTime1}us, 步骤2: {intensity2}/{exposureTime2}us");
                            ringLightResult = true;
                        }
                        else
                        {
                            LogMessage($"❌ 环形光强度和曝光时间写入失败 - 错误代码: {result}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"写入环形光源强度和曝光时间时出错: {ex.Message}");
            }

            // 写入同轴光参数到192.168.1.20（调用新的更新方法）
            try
            {
                if (!isCoaxialLightControllerConnected)
                {
                    LogMessage("⚠️ 同轴光源控制器未连接，跳过同轴光曝光时间写入");
                }
                else
                {
                    // 更新同轴光控制器的SEQ表（包括45度、0度和同轴光）
                    UpdateCoaxialLightSEQTable();
                    coaxialLightResult = true;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"写入同轴光曝光时间时出错: {ex.Message}");
            }

            // 返回是否至少有一个写入成功
            return ringLightResult || coaxialLightResult;
        }
        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        private void LogMessage(string message)
        {
            try
            {
                // 添加时间戳
                string logMessage = $"[{DateTime.Now:yy-MM-dd HH:mm:ss.fff}] {message}";
                
                // 输出到控制台
                Console.WriteLine(logMessage);
                
                // 如果Page1实例可用，也更新到Page1的日志
                LogManager.Info(message);
            }
            catch (Exception ex)
            {
                // 避免日志记录本身出错
                Console.WriteLine($"记录日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前设置的相机参数
        /// </summary>
        /// <returns>包含所有相机参数的元组</returns>
        public (int FlyingExposure, int FlyingDelay, int Fixed1Time, int Fixed2Time, bool Enable45Degree, bool Enable0Degree, int LidImageSelection, int CoatingImageSelection) GetCurrentCameraSettings()
        {
            try
            {
                int.TryParse(FlyingExposureTextBox.Text, out int flyingExposure);
                int.TryParse(FlyingDelayTextBox.Text, out int flyingDelay);
                int.TryParse(Fixed1ExposureTimeTextBox.Text, out int fixed1Time);
                int.TryParse(Fixed2ExposureTimeTextBox.Text, out int fixed2Time);

                // 获取45度和0度光使能状态
                bool enable45Degree = Enable45DegreeCheckBox?.IsChecked ?? true; // 默认为启用
                bool enable0Degree = Enable0DegreeCheckBox?.IsChecked ?? true;   // 默认为启用

                return (flyingExposure, flyingDelay, fixed1Time, fixed2Time, enable45Degree, enable0Degree, lidImageSelection, coatingImageSelection);
            }
            catch (Exception ex)
            {
                LogMessage($"获取相机参数设置失败: {ex.Message}");
                return (8, 0, 1500, 1500, true, true, 2, 3); // 返回默认值，环形光时间1500，45度和0度光默认启用，BLK图选择=2，镀膜图选择=3
            }
        }

        /// <summary>
        /// 获取当前设置的相机参数（包含同轴曝光时间）
        /// </summary>
        public (int FlyingExposure, int FlyingDelay, int Fixed1Time, int Fixed2Time, int Fixed1Coaxial, int Fixed2Coaxial, bool Enable45Degree, bool Enable0Degree, int LidImageSelection, int CoatingImageSelection) GetCurrentCameraSettingsWithCoaxial()
        {
            var basic = GetCurrentCameraSettings();
            int.TryParse(Fixed1CoaxialTimeTextBox?.Text, out int coaxial1);
            int.TryParse(Fixed2CoaxialTimeTextBox?.Text, out int coaxial2);
            return (basic.FlyingExposure, basic.FlyingDelay, basic.Fixed1Time, basic.Fixed2Time,
                coaxial1, coaxial2, basic.Enable45Degree, basic.Enable0Degree, basic.LidImageSelection, basic.CoatingImageSelection);
        }

        /// <summary>
        /// 复位光源控制器SEQ指针（只复位指针，不重写SEQ表）。
        /// 长连接模式下直接调用ResetSEQ，确保每次检测从SEQ首步开始。
        /// </summary>
        public void ResetLightControllerSeq()
        {
            try
            {
                const int moduleIndex = 1;

                // 环形光源控制器（192.168.1.16）
                if (!isLightControllerConnected)
                {
                    InitializeLightController();
                }

                if (isLightControllerConnected)
                {
                    int ringRet = optController.ResetSEQ(moduleIndex);
                    if (ringRet == OPTControllerAPI.OPT_SUCCEED)
                    {
                        LogMessage($"环形光源SEQ指针已复位 - 模块{moduleIndex}");
                    }
                    else
                    {
                        LogMessage($"环形光源SEQ复位失败 - 模块{moduleIndex}, 错误代码: {ringRet}");
                    }
                }
                else
                {
                    LogMessage("环形光源控制器未连接，跳过SEQ复位");
                }

                // 同轴光源控制器（192.168.1.20）
                if (!isCoaxialLightControllerConnected)
                {
                    InitializeCoaxialLightController();
                }

                if (isCoaxialLightControllerConnected)
                {
                    int coaxialRet = coaxialOptController.ResetSEQ(moduleIndex);
                    if (coaxialRet == OPTControllerAPI.OPT_SUCCEED)
                    {
                        LogMessage($"同轴光源SEQ指针已复位 - 模块{moduleIndex}");
                    }
                    else
                    {
                        LogMessage($"同轴光源SEQ复位失败 - 模块{moduleIndex}, 错误代码: {coaxialRet}");
                    }
                }
                else
                {
                    LogMessage("同轴光源控制器未连接，跳过SEQ复位");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"复位光源SEQ指针时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置相机参数
        /// </summary>
        /// <param name="flyingExposure">飞拍相机曝光时间</param>
        /// <param name="flyingDelay">飞拍相机延迟时间</param>
        /// <param name="fixed1Time">定拍相机1曝光时间</param>
        /// <param name="fixed2Time">定拍相机2曝光时间</param>
        public void SetCameraSettings(int flyingExposure, int flyingDelay, int fixed1Time, int fixed2Time,
            int fixed1CoaxialTime = 0, int fixed2CoaxialTime = 0,
            bool enable45Degree = true, bool enable0Degree = true, int lidSelection = 2, int coatingSelection = 3)
        {
            try
            {
                // 设置飞拍相机曝光时间
                FlyingExposureSlider.Value = Math.Max(FlyingExposureSlider.Minimum,
                    Math.Min(FlyingExposureSlider.Maximum, flyingExposure));
                FlyingExposureTextBox.Text = flyingExposure.ToString();

                // 设置飞拍相机延迟时间
                FlyingDelaySlider.Value = Math.Max(FlyingDelaySlider.Minimum,
                    Math.Min(FlyingDelaySlider.Maximum, flyingDelay));
                FlyingDelayTextBox.Text = flyingDelay.ToString();

                // 设置定拍相机1曝光时间
                Fixed1ExposureTimeSlider.Value = Math.Max(Fixed1ExposureTimeSlider.Minimum,
                    Math.Min(Fixed1ExposureTimeSlider.Maximum, fixed1Time));
                Fixed1ExposureTimeTextBox.Text = fixed1Time.ToString();

                // 设置定拍相机2曝光时间
                Fixed2ExposureTimeSlider.Value = Math.Max(Fixed2ExposureTimeSlider.Minimum,
                    Math.Min(Fixed2ExposureTimeSlider.Maximum, fixed2Time));
                Fixed2ExposureTimeTextBox.Text = fixed2Time.ToString();

                // 设置同轴光曝光时间
                if (Fixed1CoaxialTimeSlider != null && Fixed1CoaxialTimeTextBox != null)
                {
                    Fixed1CoaxialTimeSlider.Value = Math.Max(Fixed1CoaxialTimeSlider.Minimum,
                        Math.Min(Fixed1CoaxialTimeSlider.Maximum, fixed1CoaxialTime));
                    Fixed1CoaxialTimeTextBox.Text = fixed1CoaxialTime.ToString();
                }

                if (Fixed2CoaxialTimeSlider != null && Fixed2CoaxialTimeTextBox != null)
                {
                    Fixed2CoaxialTimeSlider.Value = Math.Max(Fixed2CoaxialTimeSlider.Minimum,
                        Math.Min(Fixed2CoaxialTimeSlider.Maximum, fixed2CoaxialTime));
                    Fixed2CoaxialTimeTextBox.Text = fixed2CoaxialTime.ToString();
                }

                // 设置45度和0度光使能状态
                if (Enable45DegreeCheckBox != null)
                {
                    Enable45DegreeCheckBox.IsChecked = enable45Degree;
                    is45DegreeEnabled = enable45Degree;
                }

                if (Enable0DegreeCheckBox != null)
                {
                    Enable0DegreeCheckBox.IsChecked = enable0Degree;
                    is0DegreeEnabled = enable0Degree;
                }

                // 设置图像选择状态
                lidImageSelection = lidSelection;
                coatingImageSelection = coatingSelection;

                UpdateImageSelectionState();

                // 更新按钮状态
                UpdateButtonStates();

                // 自动写入光源控制器（加载模板时），包括同轴光参数，强度固定为255
                if (!ShouldSkipLightControllerIo())
                {
                    bool lightResult = WriteLightIntensityAndTimeToController(255, fixed1Time, 255, fixed2Time,
                        fixed1CoaxialTime, fixed2CoaxialTime);
                    if (lightResult)
                    {
                        LogMessage($"✅ 模板加载时已自动写入光源参数 - 定拍1: 255/{fixed1Time}us/同轴:{fixed1CoaxialTime}us, 定拍2: 255/{fixed2Time}us/同轴:{fixed2CoaxialTime}us");
                    }
                    else
                    {
                        LogMessage($"⚠️ 模板加载时光源参数写入失败或控制器未连接");
                    }
                }

                LogMessage($"已设置相机参数 - 飞拍曝光:{flyingExposure}us, 延迟:{flyingDelay}us, 定拍1强度:255（固定）/时间:{fixed1Time}us/同轴:{fixed1CoaxialTime}us, 定拍2强度:255（固定）/时间:{fixed2Time}us/同轴:{fixed2CoaxialTime}us, BLK图选择:{lidSelection}, 镀膜图选择:{coatingSelection}");
            }
            catch (Exception ex)
            {
                LogMessage($"设置相机参数失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新渲染控件绑定
        /// </summary>
        public void RefreshRenderBindings()
        {
            try
            {
                BindRenderControls();
                LogMessage("渲染控件绑定已刷新");
            }
            catch (Exception ex)
            {
                LogMessage($"刷新渲染控件绑定失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载最后使用的相机参数
        /// </summary>
        private void LoadLastUsedCameraParameters()
        {
            try
            {
                // 获取最后使用的模板路径配置文件
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                string configFilePath = Path.Combine(configDir, "LastUsedTemplate.txt");
                
                if (File.Exists(configFilePath))
                {
                    string templatePath = File.ReadAllText(configFilePath).Trim();
                    if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                    {
                        LoadCameraParametersFromTemplate(templatePath);
                        LogMessage($"已从模板加载相机参数: {templatePath}");
                    }
                    else
                    {
                        LogMessage("最后使用的模板文件不存在，使用默认相机参数");
                    }
                }
                else
                {
                    LogMessage("未找到最后使用的模板配置，使用默认相机参数");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"加载最后使用的相机参数失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从模板文件加载相机参数
        /// </summary>
        /// <param name="templateFilePath">模板文件路径</param>
        public void LoadCameraParametersFromTemplate(string templateFilePath)
        {
            try
            {
                if (!File.Exists(templateFilePath))
                {
                    LogMessage($"模板文件不存在: {templateFilePath}");
                    return;
                }

                // 加载模板参数
                var template = TemplateParameters.LoadFromFile(templateFilePath);

                // 应用相机参数到UI
                if (template.CameraParams != null)
                {
                    // LoadFromFile方法已经处理了旧模板的兼容性转换
                    // 这里直接使用转换后的参数即可
                    // 注意：曝光强度已从模型中移除，固定使用255

                    int fixed1Time = template.CameraParams.Fixed1ExposureTime;
                    int fixed2Time = template.CameraParams.Fixed2ExposureTime;

                    // 验证参数合理性，如果异常则使用默认值
                    // 修正：允许0值，只检查负数和超出上限的情况
                    if (fixed1Time < 0 || fixed1Time > 1000) fixed1Time = 1000;
                    if (fixed2Time < 0 || fixed2Time > 1000) fixed2Time = 1000;

                    // 获取同轴光曝光时间，旧模板默认为0
                    int coaxialTime1 = template.CameraParams.Fixed1CoaxialTime;
                    int coaxialTime2 = template.CameraParams.Fixed2CoaxialTime;

                    // 获取45度和0度光使能状态，如果模板中没有（旧模板），默认都启用
                    bool enable45Degree = template.CameraParams.Enable45DegreeLight;
                    bool enable0Degree = template.CameraParams.Enable0DegreeLight;

                    // 获取图像选择状态，旧模板兼容处理
                    int lidSelection = template.CameraParams.LidImageSelection;
                    int coatingSelection = template.CameraParams.CoatingImageSelection;

                    // 兼容性检查：如果LidImageSelection或CoatingImageSelection为0（旧模板默认值）
                    // 则设置为默认值：BLK图选择=2（定拍1），镀膜图选择=3（定拍2）
                    if (lidSelection == 0)
                    {
                        lidSelection = 2;
                        LogMessage("⚠️ 旧模板兼容：BLK图选择未设置，使用默认值2（定拍1）");
                    }
                    if (coatingSelection == 0)
                    {
                        coatingSelection = 3;
                        LogMessage("⚠️ 旧模板兼容：镀膜图选择未设置，使用默认值3（定拍2）");
                    }

                    SetCameraSettings(
                        template.CameraParams.FlyingExposureTime,
                        template.CameraParams.FlyingDelayTime,
                        fixed1Time,
                        fixed2Time,
                        coaxialTime1,
                        coaxialTime2,
                        enable45Degree,
                        enable0Degree,
                        lidSelection,
                        coatingSelection
                    );

                    LogMessage($"已加载相机参数 - 飞拍曝光:{template.CameraParams.FlyingExposureTime}us, " +
                              $"延迟:{template.CameraParams.FlyingDelayTime}us, " +
                              $"定拍1强度:255（固定）/时间:{fixed1Time}us/同轴:{coaxialTime1}us, " +
                              $"定拍2强度:255（固定）/时间:{fixed2Time}us/同轴:{coaxialTime2}us, " +
                              $"45度光:{enable45Degree}, 0度光:{enable0Degree}, " +
                              $"BLK图选择:{lidSelection}, 镀膜图选择:{coatingSelection}");
                }
                else
                {
                    LogMessage("模板中未包含相机参数，使用默认值");
                }
            }
            catch (Exception ex)
            {
                // 使用Warning级别日志，相机参数加载失败不是系统级严重错误
                LogManager.Warning($"从模板加载相机参数失败: {ex.Message}", "相机配置");
            }
        }

        /// <summary>
        /// 保存当前相机参数到模板文件
        /// </summary>
        /// <param name="templateFilePath">模板文件路径</param>
        public void SaveCameraParametersToTemplate(string templateFilePath)
        {
            try
            {
                TemplateParameters template;

                // 如果模板文件存在，加载现有模板；否则创建新模板
                if (File.Exists(templateFilePath))
                {
                    template = TemplateParameters.LoadFromFile(templateFilePath);
                }
                else
                {
                    template = new TemplateParameters
                    {
                        TemplateName = "相机参数模板",
                        CreatedTime = DateTime.Now
                    };
                }

                // 获取当前相机参数设置
                var currentSettings = GetCurrentCameraSettings();

                // 更新模板中的相机参数（强度固定为255，不再从UI获取）
                template.CameraParams.FlyingExposureTime = currentSettings.FlyingExposure;
                template.CameraParams.FlyingDelayTime = currentSettings.FlyingDelay;
                template.CameraParams.Fixed1ExposureIntensity = 255; // 固定为255
                template.CameraParams.Fixed1ExposureTime = currentSettings.Fixed1Time;
                template.CameraParams.Fixed2ExposureIntensity = 255; // 固定为255
                template.CameraParams.Fixed2ExposureTime = currentSettings.Fixed2Time;

                // 保存同轴光曝光时间（如果为0或空，保持为0）
                int.TryParse(Fixed1CoaxialTimeTextBox?.Text, out int coaxialTime1);
                int.TryParse(Fixed2CoaxialTimeTextBox?.Text, out int coaxialTime2);
                template.CameraParams.Fixed1CoaxialTime = coaxialTime1;
                template.CameraParams.Fixed2CoaxialTime = coaxialTime2;

                // 保存45度和0度光使能状态
                template.CameraParams.Enable45DegreeLight = currentSettings.Enable45Degree;
                template.CameraParams.Enable0DegreeLight = currentSettings.Enable0Degree;

                // 保存图像选择状态
                template.CameraParams.LidImageSelection = currentSettings.LidImageSelection;
                template.CameraParams.CoatingImageSelection = currentSettings.CoatingImageSelection;

                // 保存模板文件
                template.SaveToFile(templateFilePath);

                LogMessage($"已保存相机参数到模板: {templateFilePath}（BLK图选择={currentSettings.LidImageSelection}, 镀膜图选择={currentSettings.CoatingImageSelection}）");
            }
            catch (Exception ex)
            {
                // 使用Warning级别日志，相机参数保存失败不是系统级严重错误
                LogManager.Warning($"保存相机参数到模板失败: {ex.Message}", "相机配置");
            }
        }

        /// <summary>
        /// 自动保存当前相机参数到最后使用的模板
        /// </summary>
        private void AutoSaveCameraParameters()
        {
            try
            {
                // 只在明确的当前模板路径下自动保存，避免写错模板
                string targetTemplatePath = TemplateConfigPage.Instance?.CurrentTemplateFilePath;

                if (string.IsNullOrWhiteSpace(targetTemplatePath) || !File.Exists(targetTemplatePath))
                {
                    LogMessage("未检测到正在编辑的模板，跳过相机参数自动保存");
                    return; // 不再回退到LastUsed，杜绝误写其它模板
                }

                SaveCameraParametersToTemplate(targetTemplatePath);
            }
            catch (Exception ex)
            {
                LogMessage($"自动保存相机参数失败: {ex.Message}");
            }
        }

        #endregion

        #region 图像选择按钮事件

        /// <summary>
        /// 飞拍相机作为LID图像按钮点击事件
        /// </summary>
        private void FlyingAsLidButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 设置LID图像选择为飞拍（1）
                lidImageSelection = 1;

                UpdateImageSelectionState();

                // 更新按钮状态
                UpdateButtonStates();

                // 自动保存到模板
                AutoSaveCameraParameters();

                LogMessage("✅ 已设置飞拍相机作为BLK图像（BLK图选择=1）");
                System.Windows.MessageBox.Show("已设置飞拍相机作为BLK图像", "设置成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"设置飞拍作为BLK图像失败: {ex.Message}");
                System.Windows.MessageBox.Show($"设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 飞拍相机作为镀膜图像按钮点击事件
        /// </summary>
        private void FlyingAsCoatingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 设置镀膜图像选择为飞拍（1）
                coatingImageSelection = 1;

                UpdateImageSelectionState();

                // 更新按钮状态
                UpdateButtonStates();

                // 自动保存到模板
                AutoSaveCameraParameters();

                LogMessage("✅ 已设置飞拍相机作为镀膜图像（镀膜图选择=1）");
                System.Windows.MessageBox.Show("已设置飞拍相机作为镀膜图像", "设置成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"设置飞拍作为镀膜图像失败: {ex.Message}");
                System.Windows.MessageBox.Show($"设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 定拍相机1作为LID图像按钮点击事件
        /// </summary>
        private void Fixed1AsLidButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 设置LID图像选择为定拍1（2）
                lidImageSelection = 2;

                UpdateImageSelectionState();

                // 更新按钮状态
                UpdateButtonStates();

                // 自动保存到模板
                AutoSaveCameraParameters();

                LogMessage("✅ 已设置定拍相机1作为BLK图像（BLK图选择=2）");
                System.Windows.MessageBox.Show("已设置定拍相机1作为BLK图像", "设置成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"设置定拍1作为BLK图像失败: {ex.Message}");
                System.Windows.MessageBox.Show($"设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 定拍相机2作为镀膜图像按钮点击事件
        /// </summary>
        private void Fixed2AsCoatingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 设置镀膜图像选择为定拍2（3）
                coatingImageSelection = 3;

                UpdateImageSelectionState();

                // 更新按钮状态
                UpdateButtonStates();

                // 自动保存到模板
                AutoSaveCameraParameters();

                LogMessage("✅ 已设置定拍相机2作为镀膜图像（镀膜图选择=3）");
                System.Windows.MessageBox.Show("已设置定拍相机2作为镀膜图像", "设置成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"设置定拍2作为镀膜图像失败: {ex.Message}");
                System.Windows.MessageBox.Show($"设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新图像选择状态到算法全局变量
        /// </summary>
        private void UpdateImageSelectionState()
        {
            try
            {
                AlgorithmGlobalVariables.Set("BLK图选择", lidImageSelection.ToString());
                AlgorithmGlobalVariables.Set("镀膜图选择", coatingImageSelection.ToString());
                LogMessage($"✅ 已更新算法全局变量 BLK图选择={lidImageSelection}, 镀膜图选择={coatingImageSelection}");
            }
            catch (Exception ex)
            {
                LogMessage($"更新图像选择失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新按钮状态（高亮显示当前选中的按钮）
        /// </summary>
        private void UpdateButtonStates()
        {
            try
            {
                // 定义颜色：灰色（未选中）和绿色（选中）
                var grayColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125)); // 灰色
                var greenColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69)); // 绿色

                // 重置所有按钮为灰色
                if (FlyingAsLidButton != null)
                    FlyingAsLidButton.Background = grayColor;

                if (FlyingAsCoatingButton != null)
                    FlyingAsCoatingButton.Background = grayColor;

                if (Fixed1AsLidButton != null)
                    Fixed1AsLidButton.Background = grayColor;

                if (Fixed2AsCoatingButton != null)
                    Fixed2AsCoatingButton.Background = grayColor;

                // 根据选择状态将对应按钮设为绿色
                // LID图像选择
                if (lidImageSelection == 1 && FlyingAsLidButton != null)
                    FlyingAsLidButton.Background = greenColor;
                else if (lidImageSelection == 2 && Fixed1AsLidButton != null)
                    Fixed1AsLidButton.Background = greenColor;

                // 镀膜图像选择
                if (coatingImageSelection == 1 && FlyingAsCoatingButton != null)
                    FlyingAsCoatingButton.Background = greenColor;
                else if (coatingImageSelection == 3 && Fixed2AsCoatingButton != null)
                    Fixed2AsCoatingButton.Background = greenColor;
            }
            catch (Exception ex)
            {
                LogMessage($"更新按钮状态失败: {ex.Message}");
            }
        }

        #endregion
    }
} 



