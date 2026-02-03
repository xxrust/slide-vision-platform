using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WpfApp2.SMTGPIO;
using WpfApp2.UI;
using WpfApp2.UI.Models;
using SplashScreen = WpfApp2.UI.SplashScreen;

namespace WpfApp2
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private SplashScreen _splashScreen;
        private static Mutex _mutex = null;
        private const string APP_MUTEX_NAME = "GlueInspectSingleInstanceMutex_{B9A7E4F2-8F3C-4A5D-9E1F-6C2D8B4A7E9F}";

        // Windows API函数声明
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// 检查是否为单实例运行
        /// </summary>
        /// <returns>如果是第一个实例返回true，否则返回false</returns>
        private bool CheckSingleInstance()
        {
            bool isFirstInstance = false;
            try
            {
                // 创建互斥体
                _mutex = new Mutex(true, APP_MUTEX_NAME, out isFirstInstance);

                if (!isFirstInstance)
                {
                    // 程序已在运行，尝试激活现有窗口
                    ActivateExistingInstance();
                    
                    // 显示提示信息
                    var systemName = WpfApp2.UI.Models.SystemBrandingManager.GetSystemName();
                    MessageBox.Show(
                        $"{systemName}已经在运行中！\n\n为避免影响工作，系统不允许同时运行多个实例。\n请使用已打开的程序窗口。",
                        "程序已运行",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                // 如果互斥体创建失败，允许程序继续运行
                MessageBox.Show(
                    $"单实例检查时发生错误，程序将继续运行：\n{ex.Message}",
                    "警告",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }
        }

        /// <summary>
        /// 激活已存在的程序实例
        /// </summary>
        private void ActivateExistingInstance()
        {
            try
            {
                // 查找现有的程序进程
                Process currentProcess = Process.GetCurrentProcess();
                Process[] processes = Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (Process process in processes)
                {
                    // 跳过当前进程
                    if (process.Id != currentProcess.Id)
                    {
                        IntPtr hWnd = process.MainWindowHandle;
                        if (hWnd != IntPtr.Zero)
                        {
                            // 如果窗口最小化，先恢复
                            if (IsIconic(hWnd))
                            {
                                ShowWindow(hWnd, SW_RESTORE);
                            }
                            
                            // 将窗口置于前台
                            SetForegroundWindow(hWnd);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 激活窗口失败不影响程序逻辑
                Debug.WriteLine($"激活现有窗口失败: {ex.Message}");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 检查是否已有实例在运行
                if (!CheckSingleInstance())
                {
                    // 程序已在运行，退出当前实例
                    Shutdown(0);
                    return;
                }

                // 首先显示启动界面
                ShowSplashScreen();

                // 全局异常处理
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnDispatcherUnhandledException;

                // 启动诊断
                StartupDiagnostic.LogInfo("程序开始启动");
                UpdateSplashProgress(10, "检查系统环境...");
                StartupDiagnostic.CheckSystemEnvironment();

                // 初始化日志管理器
                UpdateSplashProgress(20, "初始化日志系统...");
                LogManager.LoadConfigFromFile();
                LogManager.Info("应用程序启动", "Application");

                // 初始化IO控制器
                UpdateSplashProgress(40, "初始化硬件控制器...");
                try
                {
                    bool ioInitResult = IOManager.Initialize();
                    if (ioInitResult)
                    {
                        StartupDiagnostic.LogInfo("IO控制器初始化成功");
                        LogManager.Info("IO控制器初始化成功", "Hardware");
                    }
                    else
                    {
                        StartupDiagnostic.LogInfo("IO控制器初始化失败，程序将继续运行但IO功能不可用");
                        LogManager.Warning("IO控制器初始化失败，IO功能不可用", "Hardware");
                    }
                }
                catch (Exception ioEx)
                {
                    StartupDiagnostic.LogError("IO控制器初始化异常", ioEx);
                    LogManager.Error($"IO控制器初始化异常: {ioEx.Message}", "Hardware");
                }

                // 异步执行剩余的初始化步骤
                ContinueStartupAsync();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                StartupDiagnostic.LogError("程序启动过程中出现异常", ex);
                LogManager.Critical($"程序启动失败: {ex.Message}", "Application");
                
                // 关闭启动界面
                _splashScreen?.ForceClose();
                
                StartupDiagnostic.ShowErrorDialog("程序启动失败", ex);
                Shutdown(1);
            }
        }

        /// <summary>
        /// 异步继续启动过程
        /// </summary>
        private async void ContinueStartupAsync()
        {
            try
            {
                // 模拟其他初始化步骤
                UpdateSplashProgress(60, "加载用户界面...");
                await Task.Delay(500); // 模拟加载时间

                UpdateSplashProgress(80, "准备主界面...");
                await Task.Delay(300);
                
                // 检查并创建桌面快捷方式
                UpdateSplashProgress(90, "检查桌面快捷方式...");
                try
                {
                    DesktopShortcutManager.CheckAndCreateDesktopShortcut(null, false);
                }
                catch (Exception shortcutEx)
                {
                    LogManager.Warning($"桌面快捷方式创建失败: {shortcutEx.Message}", "Application");
                }
                
                UpdateSplashProgress(100, "启动完成");
                StartupDiagnostic.LogInfo("程序启动完成");
                LogManager.Info("应用程序启动完成", "Application");

                // **重要修改：在UI线程中创建并显示MainWindow**
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // 创建主窗口
                        var mainWindow = new MainWindow();
                        
                        // 关闭启动界面
                        _splashScreen?.Close();
                        
                        // 显示主窗口
                        mainWindow.Show();
                        
                        // 设置为主窗口
                        MainWindow = mainWindow;
                        
                        StartupDiagnostic.LogInfo("主窗口已创建并显示");
                    }
                    catch (Exception ex)
                    {
                        StartupDiagnostic.LogError("创建主窗口失败", ex);
                        LogManager.Critical($"创建主窗口失败: {ex.Message}", "Application");
                        Shutdown(1);
                    }
                });
            }
            catch (Exception ex)
            {
                StartupDiagnostic.LogError("异步启动过程中出现异常", ex);
                LogManager.Error($"异步启动失败: {ex.Message}", "Application");
                
                // 关闭启动界面
                _splashScreen?.ForceClose();
            }
        }

        /// <summary>
        /// 显示启动界面
        /// </summary>
        private void ShowSplashScreen()
        {
            try
            {
                _splashScreen = new SplashScreen();
                _splashScreen.Show();
            }
            catch (Exception ex)
            {
                // 启动界面显示失败不影响程序启动
                StartupDiagnostic.LogError("显示启动界面失败", ex);
            }
        }

        /// <summary>
        /// 更新启动界面进度
        /// </summary>
        /// <param name="progress">进度百分比</param>
        /// <param name="status">状态文本</param>
        private void UpdateSplashProgress(int progress, string status)
        {
            try
            {
                _splashScreen?.SetProgress(progress, status);
            }
            catch (Exception ex)
            {
                // 更新启动界面失败不影响程序启动
                StartupDiagnostic.LogError("更新启动界面进度失败", ex);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // 释放单实例互斥体
                try
                {
                    _mutex?.ReleaseMutex();
                    _mutex?.Dispose();
                    StartupDiagnostic.LogInfo("单实例互斥体已释放");
                }
                catch (Exception mutexEx)
                {
                    StartupDiagnostic.LogError("释放单实例互斥体时出现异常", mutexEx);
                }
                
                // 算法引擎已解耦：无需清理外部引擎事件处理器
                
                // 释放IO控制器资源
                try
                {
                    IOManager.Dispose();
                    StartupDiagnostic.LogInfo("IO控制器资源已释放");
                }
                catch (Exception ioEx)
                {
                    StartupDiagnostic.LogError("释放IO控制器资源时出现异常", ioEx);
                }
                
                StartupDiagnostic.LogInfo("程序正常退出");
            }
            catch (Exception ex)
            {
                StartupDiagnostic.LogError("程序退出时出现异常", ex);
            }
            finally
            {
                base.OnExit(e);
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            StartupDiagnostic.LogError("发生未处理的异常", ex);
            StartupDiagnostic.ShowErrorDialog("程序发生未处理的异常", ex);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            StartupDiagnostic.LogError("发生UI线程未处理的异常", e.Exception);
            StartupDiagnostic.ShowErrorDialog("程序发生UI异常", e.Exception);
            e.Handled = true; // 防止程序崩溃
        }
    }
}
