using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 日志查看器管理类 - 提供统一的日志查看器实现
    /// </summary>
    public static class LogViewerManager
    {
        /// <summary>
        /// 显示统一的日志查看器窗口
        /// </summary>
        /// <param name="ownerWindow">父窗口</param>
        /// <param name="windowTitle">窗口标题</param>
        /// <param name="logItems">日志数据源</param>
        /// <param name="clearLogAction">清空日志的回调方法</param>
        /// <param name="updateLogAction">更新日志的回调方法</param>
        public static void ShowLogViewer(Window ownerWindow, string windowTitle, IList logItems, 
                                        Action clearLogAction = null, Action<string> updateLogAction = null)
        {
            try
            {
                // 创建Log显示窗口
                var logWindow = new Window
                {
                    Title = $"{windowTitle} - 日志查看器（支持Ctrl+C复制）",
                    Width = 1200,
                    Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = ownerWindow
                };

                // 创建主容器
                var mainPanel = new DockPanel();

                // 创建顶部工具栏
                var toolBar = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(10, 10, 10, 5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
                };

                // 添加复制按钮
                var copyButton = new Button
                {
                    Content = "复制所有日志",
                    Width = 120,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                    Foreground = Brushes.White
                };

                // 添加复制选中项按钮
                var copySelectedButton = new Button
                {
                    Content = "复制选中项",
                    Width = 120,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                    Foreground = Brushes.White
                };

                // 添加保存到文件按钮
                var saveButton = new Button
                {
                    Content = "保存到文件",
                    Width = 120,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 124, 16)),
                    Foreground = Brushes.White
                };

                // 添加清空日志按钮
                var clearButton = new Button
                {
                    Content = "清空日志",
                    Width = 120,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(196, 43, 28)),
                    Foreground = Brushes.White
                };

                // 添加刷新按钮
                var refreshButton = new Button
                {
                    Content = "刷新显示",
                    Width = 120,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 150, 136)),
                    Foreground = Brushes.White
                };

                // 添加统计信息标签
                var statsLabel = new TextBlock
                {
                    Margin = new Thickness(20, 8, 5, 5),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(System.Windows.Media.Colors.DarkBlue)
                };

                toolBar.Children.Add(copyButton);
                toolBar.Children.Add(copySelectedButton);
                toolBar.Children.Add(saveButton);
                toolBar.Children.Add(clearButton);
                toolBar.Children.Add(refreshButton);
                toolBar.Children.Add(statsLabel);

                DockPanel.SetDock(toolBar, Dock.Top);
                mainPanel.Children.Add(toolBar);

                // 创建ListView显示log内容
                var listView = new ListView
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 250, 250)),
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas, Microsoft YaHei"),
                    Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black),
                    Margin = new Thickness(10, 5, 10, 10),
                    SelectionMode = SelectionMode.Extended // 支持多选
                };

                // 设置ListView的样式
                listView.Resources.Add(typeof(GridViewColumnHeader), new Style(typeof(GridViewColumnHeader))
                {
                    Setters = {
                        new Setter(Control.FontSizeProperty, 11.0),
                        new Setter(Control.FontWeightProperty, FontWeights.Bold)
                    }
                });

                // 创建GridView
                var gridView = new GridView();
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "时间戳",
                    Width = 180,
                    DisplayMemberBinding = new Binding("TimeStamp")
                });
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "日志消息",
                    Width = 950,
                    DisplayMemberBinding = new Binding("Message")
                });

                listView.View = gridView;
                DockPanel.SetDock(listView, Dock.Bottom);
                mainPanel.Children.Add(listView);

                logWindow.Content = mainPanel;

                // 加载日志数据的方法
                Action loadLogData = () =>
                {
                    listView.Items.Clear();
                    
                    if (logItems != null)
                    {
                        foreach (var item in logItems)
                        {
                            listView.Items.Add(item);
                        }
                    }

                    // 更新统计信息
                    UpdateStatistics(listView, statsLabel);
                    
                    // 滚动到最后一项
                    if (listView.Items.Count > 0)
                    {
                        listView.ScrollIntoView(listView.Items[listView.Items.Count - 1]);
                    }
                };

                // 初始加载数据
                loadLogData();

                // 复制所有日志按钮事件
                copyButton.Click += (s, args) =>
                {
                    try
                    {
                        var allLogs = new StringBuilder();
                        allLogs.AppendLine($"=== {windowTitle}日志导出 ===");
                        allLogs.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        allLogs.AppendLine($"日志条数: {listView.Items.Count}");
                        allLogs.AppendLine("".PadRight(50, '='));
                        allLogs.AppendLine();

                        foreach (LogEntry item in listView.Items)
                        {
                            allLogs.AppendLine($"{item.TimeStamp} | {item.Message}");
                        }

                        Clipboard.SetText(allLogs.ToString());
                        MessageBox.Show($"已复制 {listView.Items.Count} 条日志到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // 复制选中项按钮事件
                copySelectedButton.Click += (s, args) =>
                {
                    try
                    {
                        if (listView.SelectedItems.Count == 0)
                        {
                            MessageBox.Show("请先选择要复制的日志项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        var selectedLogs = new StringBuilder();
                        selectedLogs.AppendLine($"=== 选中日志导出 ({listView.SelectedItems.Count} 条) ===");
                        selectedLogs.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        selectedLogs.AppendLine("".PadRight(50, '='));
                        selectedLogs.AppendLine();

                        foreach (LogEntry item in listView.SelectedItems)
                        {
                            selectedLogs.AppendLine($"{item.TimeStamp} | {item.Message}");
                        }

                        Clipboard.SetText(selectedLogs.ToString());
                        MessageBox.Show($"已复制 {listView.SelectedItems.Count} 条选中日志到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // 保存到文件按钮事件
                saveButton.Click += (s, args) =>
                {
                    try
                    {
                        var saveDialog = new Microsoft.Win32.SaveFileDialog
                        {
                            Title = "保存日志文件",
                            Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log|所有文件 (*.*)|*.*",
                            FileName = $"{windowTitle}日志_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                        };

                        if (saveDialog.ShowDialog() == true)
                        {
                            var allLogs = new StringBuilder();
                            allLogs.AppendLine($"=== {windowTitle}日志文件 ===");
                            allLogs.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            allLogs.AppendLine($"日志条数: {listView.Items.Count}");
                            allLogs.AppendLine($"文件路径: {saveDialog.FileName}");
                            allLogs.AppendLine("".PadRight(80, '='));
                            allLogs.AppendLine();

                            foreach (LogEntry item in listView.Items)
                            {
                                allLogs.AppendLine($"{item.TimeStamp} | {item.Message}");
                            }

                            File.WriteAllText(saveDialog.FileName, allLogs.ToString(), Encoding.UTF8);
                            MessageBox.Show($"日志已保存到:\n{saveDialog.FileName}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // 清空日志按钮事件
                clearButton.Click += (s, args) =>
                {
                    try
                    {
                        var result = MessageBox.Show("确定要清空所有日志记录吗？\n\n注意：此操作将同时清空原始日志记录，无法撤销！", 
                                                   "确认清空", 
                                                   MessageBoxButton.YesNo, 
                                                   MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            // 调用清空日志的回调方法
                            clearLogAction?.Invoke();
                            
                            // 清空当前窗口的日志显示
                            listView.Items.Clear();
                            
                            // 更新统计信息
                            UpdateStatistics(listView, statsLabel);
                            
                            // 添加清空成功的提示
                            listView.Items.Add(new LogEntry 
                            { 
                                TimeStamp = DateTime.Now.ToString("yy-MM-dd HH:mm:ss.fff"), 
                                Message = "日志已清空" 
                            });
                            
                            updateLogAction?.Invoke("已清空所有日志记录");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"清空日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // 刷新显示按钮事件
                refreshButton.Click += (s, args) =>
                {
                    try
                    {
                        loadLogData();
                        updateLogAction?.Invoke($"已刷新日志显示，共 {listView.Items.Count} 条记录");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"刷新日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // 支持Ctrl+C快捷键复制选中项
                listView.KeyDown += (s, args) =>
                {
                    if (args.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        copySelectedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                };

                // 显示窗口
                logWindow.Show();
                
                updateLogAction?.Invoke($"打开增强版日志查看器，共 {listView.Items.Count} 条记录，支持复制和保存功能");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private static void UpdateStatistics(ListView listView, TextBlock statsLabel)
        {
            int totalLogs = listView.Items.Count;
            int debugLogs = 0;
            int errorLogs = 0;
            
            foreach (LogEntry item in listView.Items)
            {
                if (item.Message.Contains("[3D调试]") || item.Message.Contains("[3D保存]") || item.Message.Contains("[调试]"))
                    debugLogs++;
                if (item.Message.Contains("失败") || item.Message.Contains("错误") || item.Message.Contains("异常"))
                    errorLogs++;
            }
            
            statsLabel.Text = $"总计: {totalLogs} 条 | 调试信息: {debugLogs} 条 | 错误/异常: {errorLogs} 条";
        }
    }
} 