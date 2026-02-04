using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    public partial class SmartAnalysisFocusPage : UserControl
    {
        public event EventHandler BackRequested;
        public event EventHandler SettingsSaved;
        public event EventHandler SettingsCancelled;

        private ObservableCollection<string> _allProjects;
        private ObservableCollection<string> _focusedProjects;
        private HashSet<string> _originalFocusedProjects;

        public SmartAnalysisFocusPage()
        {
            InitializeComponent();
            InitializeCollections();
            LoadProjectData();
        }

        private void InitializeCollections()
        {
            _allProjects = new ObservableCollection<string>();
            _focusedProjects = new ObservableCollection<string>();
            _originalFocusedProjects = new HashSet<string>();

            AllProjectsListBox.ItemsSource = _allProjects;
            FocusedProjectsListBox.ItemsSource = _focusedProjects;
        }

        private void LoadProjectData()
        {
            try
            {
                LogManager.Info("[FocusPage] 开始加载项目数据");

                // 清空现有数据
                _allProjects.Clear();
                _focusedProjects.Clear();
                _originalFocusedProjects.Clear();

                // 获取所有项目名称（包括实时检测的和导入的）
                var allItemNames = SmartAnalysisMainPage.GetAllAvailableItemNames();
                LogManager.Info($"[FocusPage] 获取到 {allItemNames.Count} 个项目（包括导入项目）");

                // 加载当前关注的项目设置
                var currentFocusedProjects = FocusedProjectsManager.GetFocusedProjects();
                LogManager.Info($"[FocusPage] 当前关注项目数: {currentFocusedProjects.Count}");

                // 如果是第一次使用，默认关注所有项目
                if (currentFocusedProjects.Count == 0 && allItemNames.Count > 0)
                {
                    currentFocusedProjects = new HashSet<string>(allItemNames);
                    LogManager.Info($"[FocusPage] 首次使用，默认关注所有 {allItemNames.Count} 个项目");
                }

                // 保存原始状态
                _originalFocusedProjects = new HashSet<string>(currentFocusedProjects);

                // 填充列表
                foreach (var itemName in allItemNames)
                {
                    if (currentFocusedProjects.Contains(itemName))
                    {
                        _focusedProjects.Add(itemName);
                    }
                    else
                    {
                        _allProjects.Add(itemName);
                    }
                }

                // 更新统计显示
                UpdateCounts();

                LogManager.Info($"[FocusPage] 项目数据加载完成 - 所有项目: {_allProjects.Count}, 关注项目: {_focusedProjects.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载项目数据失败: {ex.Message}");
                MessageBox.Show($"加载项目数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCounts()
        {
            AllProjectsCountText.Text = $"共 {_allProjects.Count} 个项目";
            FocusedProjectsCountText.Text = $"共 {_focusedProjects.Count} 个关注项目";
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = AllProjectsListBox.SelectedItems.Cast<string>().ToList();
                if (!selectedItems.Any())
                {
                    MessageBox.Show("请先选择要添加的项目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var item in selectedItems)
                {
                    _allProjects.Remove(item);
                    _focusedProjects.Add(item);
                }

                UpdateCounts();
                LogManager.Info($"[FocusPage] 添加了 {selectedItems.Count} 个项目到关注列表");
            }
            catch (Exception ex)
            {
                LogManager.Error($"添加项目失败: {ex.Message}");
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = FocusedProjectsListBox.SelectedItems.Cast<string>().ToList();
                if (!selectedItems.Any())
                {
                    MessageBox.Show("请先选择要移除的项目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var item in selectedItems)
                {
                    _focusedProjects.Remove(item);
                    _allProjects.Add(item);
                }

                UpdateCounts();
                LogManager.Info($"[FocusPage] 从关注列表移除了 {selectedItems.Count} 个项目");
            }
            catch (Exception ex)
            {
                LogManager.Error($"移除项目失败: {ex.Message}");
            }
        }

        private void AddAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allItems = _allProjects.ToList();
                foreach (var item in allItems)
                {
                    _allProjects.Remove(item);
                    _focusedProjects.Add(item);
                }

                UpdateCounts();
                LogManager.Info($"[FocusPage] 添加了所有 {allItems.Count} 个项目到关注列表");
            }
            catch (Exception ex)
            {
                LogManager.Error($"添加所有项目失败: {ex.Message}");
            }
        }

        private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var focusedItems = _focusedProjects.ToList();
                foreach (var item in focusedItems)
                {
                    _focusedProjects.Remove(item);
                    _allProjects.Add(item);
                }

                UpdateCounts();
                LogManager.Info($"[FocusPage] 从关注列表移除了所有 {focusedItems.Count} 个项目");
            }
            catch (Exception ex)
            {
                LogManager.Error($"移除所有项目失败: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存关注项目设置
                var newFocusedProjects = new HashSet<string>(_focusedProjects);
                FocusedProjectsManager.SetFocusedProjects(newFocusedProjects);

                LogManager.Info($"[FocusPage] 已保存关注项目设置 - 关注项目数: {newFocusedProjects.Count}");

                // 触发设置保存事件
                SettingsSaved?.Invoke(this, EventArgs.Empty);

                MessageBox.Show($"已保存关注项目设置\n关注项目数: {newFocusedProjects.Count}", "保存成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存关注项目设置失败: {ex.Message}");
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有变更
                var currentFocusedProjects = new HashSet<string>(_focusedProjects);
                if (!currentFocusedProjects.SetEquals(_originalFocusedProjects))
                {
                    var result = MessageBox.Show("设置已修改，是否放弃更改？", "确认", 
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }
                }

                // 触发取消事件
                SettingsCancelled?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogManager.Error($"取消操作失败: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton_Click(sender, e);
        }

        /// <summary>
        /// 刷新页面数据（外部调用）
        /// </summary>
        public void RefreshData()
        {
            LoadProjectData();
        }

        /// <summary>
        /// 获取当前关注的项目列表
        /// </summary>
        public HashSet<string> GetCurrentFocusedProjects()
        {
            return new HashSet<string>(_focusedProjects);
        }
    }
} 