using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 关注项目管理器 - 负责持久化关注项目设置
    /// </summary>
    public static class FocusedProjectsManager
    {
        private static HashSet<string> _focusedProjects = new HashSet<string>();
        private static bool _isInitialized = false;
        private static readonly string _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "FocusedProjects.json");

        /// <summary>
        /// 获取关注的项目列表
        /// </summary>
        public static HashSet<string> GetFocusedProjects()
        {
            if (!_isInitialized)
            {
                LoadFromSettings();
            }
            return new HashSet<string>(_focusedProjects);
        }

        /// <summary>
        /// 设置关注的项目列表
        /// </summary>
        public static void SetFocusedProjects(HashSet<string> focusedProjects)
        {
            _focusedProjects = new HashSet<string>(focusedProjects ?? new HashSet<string>());
            SaveToSettings();
            LogManager.Info($"[FocusedProjectsManager] 已更新关注项目，数量: {_focusedProjects.Count}");
        }

        /// <summary>
        /// 检查项目是否被关注
        /// </summary>
        public static bool IsProjectFocused(string projectName)
        {
            if (!_isInitialized)
            {
                LoadFromSettings();
            }
            return _focusedProjects.Contains(projectName);
        }

        /// <summary>
        /// 添加关注项目
        /// </summary>
        public static void AddFocusedProject(string projectName)
        {
            if (!_isInitialized)
            {
                LoadFromSettings();
            }
            
            if (_focusedProjects.Add(projectName))
            {
                SaveToSettings();
                LogManager.Info($"[FocusedProjectsManager] 添加关注项目: {projectName}");
            }
        }

        /// <summary>
        /// 移除关注项目
        /// </summary>
        public static void RemoveFocusedProject(string projectName)
        {
            if (!_isInitialized)
            {
                LoadFromSettings();
            }
            
            if (_focusedProjects.Remove(projectName))
            {
                SaveToSettings();
                LogManager.Info($"[FocusedProjectsManager] 移除关注项目: {projectName}");
            }
        }

        private static void LoadFromSettings()
        {
            try
            {
                if (File.Exists(_configFile))
                {
                    var json = File.ReadAllText(_configFile, Encoding.UTF8);
                    var savedProjects = JsonConvert.DeserializeObject<List<string>>(json);
                    if (savedProjects != null)
                    {
                        _focusedProjects = new HashSet<string>(savedProjects);
                        LogManager.Info($"[FocusedProjectsManager] 从配置文件加载关注项目: {_focusedProjects.Count} 个");
                    }
                }
                
                // 如果没有保存的设置或加载失败，默认关注所有项目
                if (_focusedProjects.Count == 0)
                {
                    var allProjects = WpfApp2.UI.Controls.SmartAnalysisMainPage.GetAllAvailableItemNames();
                    if (allProjects.Count > 0)
                    {
                        _focusedProjects = new HashSet<string>(allProjects);
                        SaveToSettings(); // 保存默认设置
                        LogManager.Info($"[FocusedProjectsManager] 首次初始化，默认关注所有 {allProjects.Count} 个项目（包括导入项目）");
                    }
                }
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载关注项目设置失败: {ex.Message}");
                _focusedProjects = new HashSet<string>();
                _isInitialized = true;
            }
        }

        private static void SaveToSettings()
        {
            try
            {
                var configDir = Path.GetDirectoryName(_configFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var projectsList = _focusedProjects.ToList();
                var json = JsonConvert.SerializeObject(projectsList, Formatting.Indented);
                File.WriteAllText(_configFile, json, Encoding.UTF8);
                
                LogManager.Info($"[FocusedProjectsManager] 保存关注项目设置到 {Path.GetFileName(_configFile)}，数量: {_focusedProjects.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存关注项目设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置关注项目设置
        /// </summary>
        public static void Reset()
        {
            _focusedProjects.Clear();
            _isInitialized = false;
            
            try
            {
                if (File.Exists(_configFile))
                {
                    File.Delete(_configFile);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"删除关注项目配置文件失败: {ex.Message}");
            }
            
            LogManager.Info("[FocusedProjectsManager] 已重置关注项目设置");
        }

        /// <summary>
        /// 强制重新加载设置（用于配置变更后刷新）
        /// </summary>
        public static void Reload()
        {
            _isInitialized = false;
            LoadFromSettings();
        }
    }
} 