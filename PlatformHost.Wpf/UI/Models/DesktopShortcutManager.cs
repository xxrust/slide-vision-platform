using System;
using System.IO;
using System.Reflection;
using System.Windows;
using IWshRuntimeLibrary;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 桌面快捷方式管理器
    /// </summary>
    public static class DesktopShortcutManager
    {
        /// <summary>
        /// 检查并创建桌面快捷方式
        /// </summary>
        /// <param name="shortcutName">快捷方式名称</param>
        /// <param name="showSuccessMessage">是否显示成功创建的消息</param>
        /// <returns>true: 快捷方式已存在或创建成功; false: 创建失败</returns>
        public static bool CheckAndCreateDesktopShortcut(string shortcutName = "点胶检测系统", bool showSuccessMessage = false)
        {
            try
            {
                // 获取桌面路径
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.lnk");

                // 如果快捷方式已存在，直接返回
                if (System.IO.File.Exists(shortcutPath))
                {
                    LogManager.Info($"桌面快捷方式已存在: {shortcutPath}", "DesktopShortcut");
                    return true;
                }

                // 创建快捷方式
                bool created = CreateShortcut(shortcutPath, shortcutName);
                
                if (created)
                {
                    LogManager.Info($"桌面快捷方式创建成功: {shortcutPath}", "DesktopShortcut");
                    
                    if (showSuccessMessage)
                    {
                        MessageBox.Show($"桌面快捷方式已创建: {shortcutName}", "快捷方式创建",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    LogManager.Error($"桌面快捷方式创建失败: {shortcutPath}", "DesktopShortcut");
                }

                return created;
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查或创建桌面快捷方式时发生异常: {ex.Message}", "DesktopShortcut");
                return false;
            }
        }

        /// <summary>
        /// 创建桌面快捷方式
        /// </summary>
        /// <param name="shortcutPath">快捷方式完整路径</param>
        /// <param name="shortcutName">快捷方式名称</param>
        /// <returns>是否创建成功</returns>
        private static bool CreateShortcut(string shortcutPath, string shortcutName)
        {
            try
            {
                // 获取当前程序的路径
                string currentExePath = Assembly.GetExecutingAssembly().Location;
                string workingDirectory = Path.GetDirectoryName(currentExePath);

                // 创建WScript.Shell对象
                WshShell shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

                // 设置快捷方式属性
                shortcut.TargetPath = currentExePath;
                shortcut.WorkingDirectory = workingDirectory;
                shortcut.Description = "点胶检测系统 - 工业级点胶质量检测解决方案";
                
                // 设置图标（如果图标文件存在）
                string iconPath = Path.Combine(workingDirectory, "posen.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath;
                }

                // 保存快捷方式
                shortcut.Save();

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error($"创建快捷方式失败: {ex.Message}", "DesktopShortcut");
                return false;
            }
        }

        /// <summary>
        /// 删除桌面快捷方式
        /// </summary>
        /// <param name="shortcutName">快捷方式名称</param>
        /// <returns>是否删除成功</returns>
        public static bool RemoveDesktopShortcut(string shortcutName = "点胶检测系统")
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.lnk");

                if (System.IO.File.Exists(shortcutPath))
                {
                    System.IO.File.Delete(shortcutPath);
                    LogManager.Info($"桌面快捷方式已删除: {shortcutPath}", "DesktopShortcut");
                    return true;
                }

                return true; // 如果不存在，也认为删除成功
            }
            catch (Exception ex)
            {
                LogManager.Error($"删除桌面快捷方式失败: {ex.Message}", "DesktopShortcut");
                return false;
            }
        }

        /// <summary>
        /// 检查桌面快捷方式是否存在
        /// </summary>
        /// <param name="shortcutName">快捷方式名称</param>
        /// <returns>是否存在</returns>
        public static bool IsDesktopShortcutExists(string shortcutName = "点胶检测系统")
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.lnk");
                return System.IO.File.Exists(shortcutPath);
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查桌面快捷方式是否存在时发生异常: {ex.Message}", "DesktopShortcut");
                return false;
            }
        }
    }
} 