using System;
using System.IO;
using WpfApp2.UI.Models;

namespace WpfApp2.Utils
{
    /// <summary>
    /// VM算法方案配置管理器
    /// 用于记录和加载上次使用的sol文件路径
    /// </summary>
    public static class SolutionConfigManager
    {
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "LastUsedSolution.txt");
        private const string DefaultSolution = "双深度_V4.4.0.sol";

        /// <summary>
        /// 保存最后使用的sol文件路径
        /// </summary>
        /// <param name="solutionPath">sol文件路径</param>
        public static void SaveLastUsedSolution(string solutionPath)
        {
            try
            {
                // 确保Config目录存在
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                // 保存路径到文件
                File.WriteAllText(ConfigFile, solutionPath);
                LogManager.Info($"已保存算法文件路径: {solutionPath}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存算法文件路径失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取最后使用的sol文件路径，如果不存在则返回默认值
        /// </summary>
        /// <returns>sol文件路径</returns>
        public static string GetLastUsedSolution()
        {
            try
            {
                // 如果配置文件存在
                if (File.Exists(ConfigFile))
                {
                    string savedPath = File.ReadAllText(ConfigFile).Trim();

                    // 如果保存的路径不为空
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        LogManager.Info($"读取到上次使用的算法文件: {savedPath}");
                        return savedPath;
                    }
                }

                // 配置文件不存在或为空，返回默认值
                LogManager.Info($"使用默认算法文件: {DefaultSolution}");
                return DefaultSolution;
            }
            catch (Exception ex)
            {
                LogManager.Error($"读取算法文件路径失败: {ex.Message}，使用默认值");
                return DefaultSolution;
            }
        }

        /// <summary>
        /// 获取默认的sol文件名
        /// </summary>
        /// <returns>默认sol文件名</returns>
        public static string GetDefaultSolution()
        {
            return DefaultSolution;
        }
    }
}
