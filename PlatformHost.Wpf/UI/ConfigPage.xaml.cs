using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using WpfApp2.Models;
using static WpfApp2.UI.Page1;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class ConfigPage : Page
    {
        // 存储所有模板的列表
        private List<TemplateParameters> availableTemplates = new List<TemplateParameters>();
        
        // 存储当前选择的模板档案
        private string selectedProfileId = string.Empty;

        private List<TemplateProfileDefinition> availableProfiles = new List<TemplateProfileDefinition>();

        public ConfigPage()
        {
            InitializeComponent();
            
            // 注册页面加载事件
            this.Loaded += ConfigPage_Loaded;
        }

        /// <summary>
        /// 页面加载事件处理 - 重置界面状态
        /// </summary>
        private void ConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            LogManager.Info("ConfigPage 页面已加载");
            // 确保加载弹窗是隐藏的
            HideLoadingOverlay();
            
            // 重置到第一层界面（操作选择）
            ResetToOperationSelection();
            
            // 更新当前模板名称显示
            UpdateCurrentTemplateDisplay();

            // 加载模板档案配置
            LoadProfileCards();
        }

        private void UpdateCurrentTemplateDisplay()
        {
            try
            {
                // 获取当前模板名称
                string currentTemplateName = PageManager.Page1Instance?.CurrentTemplateName ?? "未知";
                
                // 更新显示
                if (CurrentTemplateNameDisplay != null)
                {
                    CurrentTemplateNameDisplay.Text = $"当前模板：{currentTemplateName}";
                }
                
                LogManager.Info($"当前模板显示已更新: {currentTemplateName}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新当前模板显示失败: {ex.Message}");
            }
        }

        private void LoadProfileCards()
        {
            try
            {
                availableProfiles = TemplateHierarchyConfig.Instance.Profiles ?? new List<TemplateProfileDefinition>();
                var slots = new[]
                {
                    (Title: ProfileCard1Title, Description: ProfileCard1Description, Button: ProfileCard1Button),
                    (Title: ProfileCard2Title, Description: ProfileCard2Description, Button: ProfileCard2Button),
                    (Title: ProfileCard3Title, Description: ProfileCard3Description, Button: ProfileCard3Button)
                };

                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    var profile = i < availableProfiles.Count ? availableProfiles[i] : null;
                    if (profile == null)
                    {
                        slot.Title.Text = "Unavailable";
                        slot.Description.Text = "No profile configured.";
                        slot.Button.IsEnabled = false;
                        slot.Button.Tag = null;
                        continue;
                    }

                    slot.Title.Text = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Id : profile.DisplayName;
                    slot.Description.Text = profile.Description ?? string.Empty;
                    slot.Button.IsEnabled = true;
                    slot.Button.Tag = profile.Id;
                }

                if (availableProfiles.Count > slots.Length)
                {
                    LogManager.Info($"模板档案超过可显示数量: {availableProfiles.Count} (仅显示前 {slots.Length} 个)");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载模板档案失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置到操作选择界面
        /// </summary>
        private void ResetToOperationSelection()
        {
            // 显示操作选择界面，隐藏其他界面
            OperationSelectionGrid.Visibility = Visibility.Visible;
            SampleTypeSelectionGrid.Visibility = Visibility.Collapsed;
            TemplateListGrid.Visibility = Visibility.Collapsed;

            // 恢复原始标题和提示
            TitleText.Text = "模板配置";
            SubtitleText.Text = "请选择您要进行的操作";
            FooterText.Text = "选择操作类型开始配置检测模板";

            // 更新智能返回按钮文字
            UpdateSmartBackButton();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 智能返回：根据当前界面状态决定返回行为
            if (SampleTypeSelectionGrid.Visibility == Visibility.Visible || 
                     TemplateListGrid.Visibility == Visibility.Visible)
            {
                // 当前在第二层，返回到第一层（操作选择界面）
                BackToOperationSelection();
            }
            else
            {
                // 🔧 关键修复：返回主页时重置检测管理器状态
                WpfApp2.UI.Page1.PageManager.ResetDetectionManagerOnPageReturn("配置页面");

                // 当前在第一层，返回到主页
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                mainWindow.ContentC.Content = mainWindow.frame1; // 返回到Page1
            }
        }

        /// <summary>
        /// 返回到操作选择界面
        /// </summary>
        private void BackToOperationSelection()
        {
            // 确保隐藏加载弹窗（如果有的话）
            HideLoadingOverlay();
            
            // 重置到操作选择界面
            ResetToOperationSelection();
        }

        /// <summary>
        /// 返回到样品类型选择界面
        /// </summary>
        private void BackToSampleTypeSelection()
        {
            // 显示样品类型选择界面，隐藏其他界面
            OperationSelectionGrid.Visibility = Visibility.Collapsed;
            SampleTypeSelectionGrid.Visibility = Visibility.Visible;
            TemplateListGrid.Visibility = Visibility.Collapsed;

            // 更新标题和提示
            TitleText.Text = "创建新模板";
            SubtitleText.Text = "请选择模板档案";
            FooterText.Text = "选择档案后将进入对应的配置流程，不同档案使用不同的参数与步骤";

            // 更新智能返回按钮
            UpdateSmartBackButton();
        }

        /// <summary>
        /// 更新智能返回按钮的文字
        /// </summary>
        private void UpdateSmartBackButton()
        {
            if (SampleTypeSelectionGrid.Visibility == Visibility.Visible || 
                TemplateListGrid.Visibility == Visibility.Visible)
            {
                // 第二层或第三层：显示返回上一步
                SmartBackButton.Content = "← 返回上一步";
            }
            else
            {
                // 第一层：显示返回主页
                SmartBackButton.Content = "← 返回主页";
            }
        }

        #region 第一阶段：操作类型选择

        /// <summary>
        /// 创建新模板按钮点击事件
        /// </summary>
        private void NewTemplate_Click(object sender, RoutedEventArgs e)
        {
            // 切换到样品类型选择界面
            OperationSelectionGrid.Visibility = Visibility.Collapsed;
            SampleTypeSelectionGrid.Visibility = Visibility.Visible;
            TemplateListGrid.Visibility = Visibility.Collapsed;

            // 更新标题和提示
            TitleText.Text = "创建新模板";
            SubtitleText.Text = "请选择模板档案";
            FooterText.Text = "选择档案后将进入对应的配置流程，不同档案使用不同的参数与步骤";

            // 更新智能返回按钮
            UpdateSmartBackButton();
        }

        /// <summary>
        /// 加载现有模板按钮点击事件
        /// </summary>
        private void LoadExistingTemplate_Click(object sender, RoutedEventArgs e)
        {
            // 切换到模板列表界面
            OperationSelectionGrid.Visibility = Visibility.Collapsed;
            SampleTypeSelectionGrid.Visibility = Visibility.Collapsed;
            TemplateListGrid.Visibility = Visibility.Visible;

            // 更新标题和提示
            TitleText.Text = "加载现有模板";
            SubtitleText.Text = "从已保存的模板中选择";
            FooterText.Text = "选择要加载的模板继续使用之前配置好的检测参数";

            // 更新智能返回按钮
            UpdateSmartBackButton();

            // 加载模板列表
            LoadTemplateList();
        }

        /// <summary>
        /// 操作类型卡片鼠标悬停效果
        /// </summary>
        private void OperationCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                // 添加悬停效果 - 轻微放大和阴影
                border.RenderTransform = new ScaleTransform(1.05, 1.05);
                border.RenderTransformOrigin = new Point(0.5, 0.5);
                
                // 添加阴影效果
                border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Gray,
                    BlurRadius = 15,
                    ShadowDepth = 8,
                    Opacity = 0.4
                };
            }
        }

        /// <summary>
        /// 操作类型卡片鼠标离开效果
        /// </summary>
        private void OperationCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                // 恢复原始大小
                border.RenderTransform = new ScaleTransform(1.0, 1.0);
                
                // 移除阴影效果
                border.Effect = null;
            }
        }

        #endregion

        /// <summary>
        /// 打开当前模板按钮点击事件
        /// </summary>
        private async void OpenCurrentTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取当前模板名称
                string currentTemplateName = PageManager.Page1Instance?.CurrentTemplateName ?? "";
                
                if (string.IsNullOrEmpty(currentTemplateName) || currentTemplateName == "未知")
                {
                    MessageBox.Show("当前没有可用的模板，请先加载一个模板。", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 构建当前模板的文件路径
                string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                string templateFilePath = Path.Combine(templatesDir, $"{currentTemplateName}.json");

                if (!File.Exists(templateFilePath))
                {
                    MessageBox.Show($"模板文件不存在：{currentTemplateName}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 加载模板信息
                var template = TemplateParameters.LoadFromFile(templateFilePath);

                // 获取MainWindow实例
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                TrySyncCameraParametersToTemplate(mainWindow, templateFilePath);
                
                // 创建对应样品类型和涂布类型的模板配置页面
                var templateConfigPage = mainWindow.CreateTemplateConfigPage(template.ProfileId);

                // 切换到模板配置页面
                mainWindow.ContentC.Content = mainWindow.frame_TemplateConfigPage;

                // 🔧 关键修复：设置系统状态为模板配置模式
                if (PageManager.Page1Instance?.DetectionManager != null)
                {
                    PageManager.Page1Instance.DetectionManager.SetSystemState(SystemDetectionState.TemplateConfiguring);
                    LogManager.Info("已设置系统状态为模板配置模式 - 图片匹配将只需要2D完成");
                }

                // 加载模板到页面
                templateConfigPage.LoadTemplate(templateFilePath);

                // 记录日志
                LogManager.Info($"已打开当前模板配置: {currentTemplateName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开当前模板配置时出错: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Error($"打开当前模板配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 样品类型选择事件处理
        /// </summary>
        private void SelectSampleType_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag != null)
                {
                    selectedProfileId = button.Tag.ToString();
                    var profile = TemplateHierarchyConfig.Instance.ResolveProfile(selectedProfileId);
                    var profileName = profile?.DisplayName ?? selectedProfileId;
                    var profileDescription = profile?.Description ?? string.Empty;

                    var confirmMessage = string.IsNullOrWhiteSpace(profileDescription)
                        ? $"您的选择：{profileName}\n\n确定要创建此档案的新模板吗？"
                        : $"您的选择：{profileName}\n{profileDescription}\n\n确定要创建此档案的新模板吗？";

                    MessageBoxResult result = MessageBox.Show(
                        confirmMessage,
                        "确认模板档案",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        ShowLoadingOverlay($"正在创建 {profileName} 模板配置页面...");

                        try
                        {
                            // 异步创建模板配置页面
                            Task.Run(() => Task.Delay(500).Wait()).Wait();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var mainWindow = (MainWindow)Application.Current.MainWindow;
                                var templateConfigPage = mainWindow.CreateTemplateConfigPage(selectedProfileId);
                                mainWindow.ContentC.Content = mainWindow.frame_TemplateConfigPage;
                                LogManager.Info($"开始配置新模板 - 模板档案: {profileName}");
                            });
                        }
                        finally
                        {
                            HideLoadingOverlay();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择模板档案时出错: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Info($"选择模板档案失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 样品类型卡片鼠标悬停效果
        /// </summary>
        private void SampleTypeCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                // 添加悬停效果 - 轻微放大和阴影
                border.RenderTransform = new ScaleTransform(1.02, 1.02);
                border.RenderTransformOrigin = new Point(0.5, 0.5);
                
                // 添加阴影效果
                border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Gray,
                    BlurRadius = 10,
                    ShadowDepth = 5,
                    Opacity = 0.3
                };
            }
        }

        /// <summary>
        /// 样品类型卡片鼠标离开效果
        /// </summary>
        private void SampleTypeCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                // 恢复原始大小
                border.RenderTransform = new ScaleTransform(1.0, 1.0);
                
                // 移除阴影效果
                border.Effect = null;
            }
        }

        /// <summary>
        /// 加载现有模板列表
        /// </summary>
        private void LoadTemplateList()
        {
            try
            {
                // 获取模板目录
                string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");

                // 如果目录不存在则创建
                if (!Directory.Exists(templatesDir))
                {
                    Directory.CreateDirectory(templatesDir);
                    MessageBox.Show($"已创建模板目录: {templatesDir}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // 清空现有列表
                TemplateListBox.Items.Clear();
                availableTemplates.Clear();

                // 获取目录中的所有JSON文件
                string[] templateFiles = Directory.GetFiles(templatesDir, "*.json");

                if (templateFiles.Length == 0)
                {
                    // 如果没有模板，添加提示
                    TextBlock noTemplatesMsg = new TextBlock
                    {
                        Text = "没有可用的模板，请先创建模板",
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(10)
                    };

                    TemplateListBox.Items.Add(noTemplatesMsg);
                    MessageBox.Show("未找到可用模板，请先创建模板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 动态构建模板卡片的网格，按列数自动扩展行数
                Grid templateGrid = new Grid
                {
                    Margin = new Thickness(10)
                };

                double rowHeight = 240;   // 固定每行的高度，方便展示长名称和参数信息
                int columns = 6;

                for (int i = 0; i < columns; i++)
                {
                    templateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
                templateGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });

                int row = 0;
                int col = 0;


                // 加载每个模板
                foreach (string file in templateFiles)
                {
                    if (col >= columns)
                    {
                        col = 0;
                        row++;

                        if (row >= templateGrid.RowDefinitions.Count)
                        {
                            templateGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });
                        }
                    }


                    try
                    {
                        TemplateParameters template = TemplateParameters.LoadFromFile(file);
                        availableTemplates.Add(template);

                        // 创建一个包含模板信息的卡片式UI元素
                        Border templateCard = new Border
                        {
                            BorderBrush = new SolidColorBrush(Colors.DarkGray),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(5),
                            Margin = new Thickness(8),
                            Background = new SolidColorBrush(Colors.WhiteSmoke),
                            Height = rowHeight - 20, // 留出一些间距
                        };

                        // 使卡片能够自动填充可用空间
                        templateCard.HorizontalAlignment = HorizontalAlignment.Stretch;
                        templateCard.VerticalAlignment = VerticalAlignment.Stretch;

                        StackPanel templateItem = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(10),
                            HorizontalAlignment = HorizontalAlignment.Center
                        };

                        // 添加模板名称
                        TextBlock nameText = new TextBlock
                        {
                            Text = template.TemplateName,
                            FontSize = 16,
                            FontWeight = FontWeights.Bold,
                            TextWrapping = TextWrapping.Wrap,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 5)
                        };
                        templateItem.Children.Add(nameText);

                        // 添加模板档案信息
                        var profile = TemplateHierarchyConfig.Instance.ResolveProfile(template.ProfileId);
                        var profileDisplayName = profile?.DisplayName ?? template.ProfileId ?? "Unknown";
                        TextBlock profileText = new TextBlock
                        {
                            Text = $"档案: {profileDisplayName}",
                            FontSize = 12,
                            Foreground = new SolidColorBrush(Colors.Blue),
                            Margin = new Thickness(0, 5, 0, 5),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center
                        };
                        templateItem.Children.Add(profileText);

                        // 添加最后修改时间
                        TextBlock timeText = new TextBlock
                        {
                            Text = $"最后更新: {template.LastModifiedTime:yyyy-MM-dd HH:mm}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Colors.Gray),
                            Margin = new Thickness(0, 5, 0, 5),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center
                        };
                        templateItem.Children.Add(timeText);

                        // 添加备注(如果有)
                        if (!string.IsNullOrEmpty(template.Remark))
                        {
                            TextBlock remark = new TextBlock
                            {
                                Text = $"备注: {template.Remark}",
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 5, 0, 10),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextAlignment = TextAlignment.Center,
                                Height = 40 // 限制备注最大高度
                            };
                            templateItem.Children.Add(remark);
                        }
                        else
                        {
                            // 如果没有备注，添加一个空的TextBlock保持布局一致
                            TextBlock emptyRemark = new TextBlock
                            {
                                Text = "",
                                Height = 40,
                                Margin = new Thickness(0, 5, 0, 10)
                            };
                            templateItem.Children.Add(emptyRemark);
                        }

                        // 创建按钮面板
                        StackPanel buttonPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 5, 0, 0)
                        };

                        // 添加加载按钮
                        Button loadButton = new Button
                        {
                            Content = "加载模板",
                            Tag = file, // 保存文件路径
                            Margin = new Thickness(0, 0, 5, 0),
                            Padding = new Thickness(8, 3, 8, 3),
                            Background = new SolidColorBrush(Colors.Green),
                            Foreground = new SolidColorBrush(Colors.White),
                            FontSize = 11
                        };
                        loadButton.Click += LoadTemplate_Click;
                        buttonPanel.Children.Add(loadButton);

                        // 添加删除按钮
                        Button deleteButton = new Button
                        {
                            Content = "删除",
                            Tag = file, // 保存文件路径
                            Margin = new Thickness(5, 0, 0, 0),
                            Padding = new Thickness(8, 3, 8, 3),
                            Background = new SolidColorBrush(Colors.Red),
                            Foreground = new SolidColorBrush(Colors.White),
                            FontSize = 11
                        };
                        deleteButton.Click += DeleteTemplate_Click;
                        buttonPanel.Children.Add(deleteButton);

                        templateItem.Children.Add(buttonPanel);
                        templateCard.Child = templateItem;

                        // 将模板卡片添加到网格的当前位置
                        Grid.SetRow(templateCard, row);
                        Grid.SetColumn(templateCard, col);
                        templateGrid.Children.Add(templateCard);

                        // 更新行列位置
                        col++;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载模板文件错误: {file}\n错误: {ex.Message}", "加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 将网格添加到TemplateListBox
                TemplateListBox.Items.Add(templateGrid);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载模板列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载选中的模板
        /// </summary>
        private async void LoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            // 获取点击的按钮
            Button loadButton = sender as Button;
            if (loadButton == null || loadButton.Tag == null) return;

            // 从按钮的Tag获取模板文件路径
            string templateFilePath = loadButton.Tag.ToString();

            try
            {
                // 先加载模板以获取样品类型和名称
                var template = TemplateParameters.LoadFromFile(templateFilePath);
                
                // 显示加载弹窗
                ShowLoadingOverlay($"正在加载模板: {template.TemplateName}...");

                try
                {
                    // 异步处理加载过程
                    await Task.Run(() =>
                    {
                        // 模拟加载时间
                        Task.Delay(300).Wait();
                    });

                    // 在UI线程中执行页面创建和导航
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // 获取MainWindow实例
                        var mainWindow = (MainWindow)Application.Current.MainWindow;
                        TrySyncCameraParametersToTemplate(mainWindow, templateFilePath);
                        
                        // 使用MainWindow的方法创建对应样品类型和涂布类型的模板配置页面
                        var templateConfigPage = mainWindow.CreateTemplateConfigPage(template.ProfileId);

                        // 切换到模板配置页面（使用Frame而不是直接的Page）
                        mainWindow.ContentC.Content = mainWindow.frame_TemplateConfigPage;

                        // 🔧 关键修复：设置系统状态为模板配置模式
                        if (PageManager.Page1Instance?.DetectionManager != null)
                        {
                            PageManager.Page1Instance.DetectionManager.SetSystemState(SystemDetectionState.TemplateConfiguring);
                            LogManager.Info("已设置系统状态为模板配置模式 - 图片匹配将只需要2D完成");
                        }

                        // 加载模板到页面
                        templateConfigPage.LoadTemplate(templateFilePath);

                        // 隐藏模板列表
                        TemplateListGrid.Visibility = Visibility.Collapsed;

                        // 记录日志
                        var profile = TemplateHierarchyConfig.Instance.ResolveProfile(template.ProfileId);
                        var profileDisplayName = profile?.DisplayName ?? template.ProfileId ?? "Unknown";
                        LogManager.Info(
                            $"已加载模板: {template.TemplateName} (档案: {profileDisplayName})");
                    });

                    // 短暂延迟后隐藏加载弹窗
                    await Task.Delay(200);
                    HideLoadingOverlay();
                }
                catch (Exception _)
                {
                    // 出错时隐藏加载弹窗
                    HideLoadingOverlay();
                    throw; // 重新抛出异常以便外层处理
                }
            }
            catch (Exception ex)
            {
                // 确保出错时隐藏加载弹窗
                HideLoadingOverlay();
                MessageBox.Show($"加载模板失败: {ex.Message}", "加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Info($"加载模板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除选中的模板
        /// </summary>
        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            // 获取点击的按钮
            Button deleteButton = sender as Button;
            if (deleteButton == null || deleteButton.Tag == null) return;

            // 从按钮的Tag获取模板文件路径
            string templateFilePath = deleteButton.Tag.ToString();
            string templateName = Path.GetFileNameWithoutExtension(templateFilePath);

            // 弹出确认对话框
            MessageBoxResult result = MessageBox.Show(
                $"确定要删除模板 \"{templateName}\" 吗？\n此操作不可恢复。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // 如果用户确认删除
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 删除模板文件
                    File.Delete(templateFilePath);

                    // 显示删除成功消息
                    MessageBox.Show($"已成功删除模板 \"{templateName}\"", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 记录日志
                    LogManager.Info($"已删除模板: {templateName}");

                    // 重新加载模板列表以反映变化
                    LoadTemplateList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除模板失败: {ex.Message}", "删除错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogManager.Info($"删除模板失败: {ex.Message}");
                }
            }
        }

        #region 加载弹窗控制

        /// <summary>
        /// 显示加载弹窗
        /// </summary>
        /// <param name="message">加载提示信息</param>
        private void ShowLoadingOverlay(string message = "正在创建模板配置页面...")
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            
            // 强制更新UI
            Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// 隐藏加载弹窗
        /// </summary>
        private void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion

        /// <summary>
        /// 在切换至模板配置页面前尝试将当前相机参数写入目标模板，避免未应用的调整丢失。
        /// </summary>
        /// <param name="mainWindow">主窗口实例，用于访问相机配置页面。</param>
        /// <param name="templateFilePath">模板文件完整路径。</param>
        private void TrySyncCameraParametersToTemplate(MainWindow mainWindow, string templateFilePath)
        {
            // 仅当目标模板就是当前正在编辑的模板时才同步，避免跨模板误写
            if (mainWindow?.frame_CameraConfigPage?.Content is CameraConfigPage cameraConfigPage &&
                !string.IsNullOrWhiteSpace(templateFilePath) &&
                TemplateConfigPage.Instance != null &&
                string.Equals(TemplateConfigPage.Instance.CurrentTemplateFilePath, templateFilePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    cameraConfigPage.SaveCameraParametersToTemplate(templateFilePath);
                    LogManager.Info($"已同步当前相机参数到模板: {templateFilePath}");
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"同步相机参数到模板失败: {ex.Message}", "模板配置");
                }
            }
        }
    }
}
