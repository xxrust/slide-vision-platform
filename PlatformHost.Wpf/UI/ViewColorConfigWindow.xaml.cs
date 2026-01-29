using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp2.UI.Models;
using WpfApp2.Models;
using System.Globalization;
using Keyence.LjDev3dView;

namespace WpfApp2.UI
{
    /// <summary>
    /// ViewColorConfigWindow.xaml 的交互逻辑
    /// 基恩士2D和3D视图颜色配置窗口 - 统一配置版本
    /// </summary>
    public partial class ViewColorConfigWindow : Window
    {
        private Page1 _parentPage;
        private bool _isLoading = false; // 防止加载时触发事件
        
        /// <summary>
        /// 简化的颜色配置类
        /// </summary>
        public class SimpleColorConfig
        {
            public bool UseCustomColorRange = false;
            public double ColorRangeMin = -1.5;
            public double ColorRangeMax = 1.5;
            public double MeshTransparent = 0.5;
            public double BlendWeight = 0.5;
            public bool DisplayColorBar = true;
            public bool DisplayGrid = true;
            public bool DisplayAxis = true;
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="parentPage">父级Page1实例，用于应用配置</param>
        public ViewColorConfigWindow(Page1 parentPage)
        {
            try
            {
                InitializeComponent();
                _parentPage = parentPage;
                
                // 延迟加载配置，确保窗口完全加载
                this.Loaded += Window_Loaded;
                
                string currentTemplate = GetCurrentTemplateName();
                LogManager.Info($"颜色配置窗口初始化完成，当前模板: {currentTemplate}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"颜色配置窗口初始化失败: {ex.Message}");
                MessageBox.Show($"颜色配置窗口初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取当前模板名称
        /// </summary>
        private string GetCurrentTemplateName()
        {
            try
            {
                return _parentPage?.CurrentTemplateName ?? "Default";
            }
            catch (Exception ex)
            {
                LogManager.Warning($"获取模板名称失败: {ex.Message}");
                return "Default";
            }
        }

        /// <summary>
        /// 获取当前模板文件路径
        /// </summary>
        private string GetCurrentTemplateFilePath()
        {
            try
            {
                string templateName = GetCurrentTemplateName();
                string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                return Path.Combine(templatesDir, $"{templateName}.json");
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取模板文件路径失败: {ex.Message}");
                return null;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadCurrentSettings();
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载颜色配置失败: {ex.Message}");
                SetDefaultValues();
            }
        }

        /// <summary>
        /// 从模板文件加载颜色配置
        /// </summary>
        private SimpleColorConfig LoadConfigFromTemplate()
        {
            var config = new SimpleColorConfig();
            
            try
            {
                string templatePath = GetCurrentTemplateFilePath();
                if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                {
                    var template = TemplateParameters.LoadFromFile(templatePath);
                    if (template?.ColorParams != null)
                    {
                        config.UseCustomColorRange = template.ColorParams.UseCustomColorRange;
                        config.ColorRangeMin = template.ColorParams.ColorRangeMin;
                        config.ColorRangeMax = template.ColorParams.ColorRangeMax;
                        config.MeshTransparent = template.ColorParams.MeshTransparent;
                        config.BlendWeight = template.ColorParams.BlendWeight;
                        config.DisplayColorBar = template.ColorParams.DisplayColorBar;
                        config.DisplayGrid = template.ColorParams.DisplayGrid;
                        config.DisplayAxis = template.ColorParams.DisplayAxis;
                        
                        LogManager.Info($"从模板加载颜色配置: {GetCurrentTemplateName()}");
                    }
                    else
                    {
                        LogManager.Info("模板中无颜色配置，使用默认配置");
                    }
                }
                else
                {
                    LogManager.Warning($"模板文件不存在: {templatePath}，使用默认配置");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"从模板加载颜色配置失败: {ex.Message}");
            }
            
            return config;
        }

        /// <summary>
        /// 加载当前配置到界面
        /// </summary>
        private void LoadCurrentSettings()
        {
            _isLoading = true;
            try
            {
                var config = LoadConfigFromTemplate();
                ApplyConfigToUI(config);
                SyncToPage1Config(config);
                LogManager.Info("颜色配置加载完成");
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载颜色配置失败: {ex.Message}");
                SetDefaultValues();
            }
            finally
            {
                _isLoading = false;
            }
        }



        /// <summary>
        /// 将配置应用到界面
        /// </summary>
        private void ApplyConfigToUI(SimpleColorConfig config)
        {
            try
            {
                if (UseCustomColorRange != null) UseCustomColorRange.IsChecked = config.UseCustomColorRange;
                if (ColorRangeMinSlider != null) ColorRangeMinSlider.Value = config.ColorRangeMin;
                if (ColorRangeMaxSlider != null) ColorRangeMaxSlider.Value = config.ColorRangeMax;
                if (MeshTransparentSlider != null) MeshTransparentSlider.Value = config.MeshTransparent;
                if (BlendWeightSlider != null) BlendWeightSlider.Value = config.BlendWeight;
                if (DisplayColorBar != null) DisplayColorBar.IsChecked = config.DisplayColorBar;
                if (DisplayGrid != null) DisplayGrid.IsChecked = config.DisplayGrid;
                if (DisplayAxis != null) DisplayAxis.IsChecked = config.DisplayAxis;

                // 更新界面状态
                if (RangeSettings != null) RangeSettings.IsEnabled = config.UseCustomColorRange;
                UpdateDisplayValues();
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用配置到界面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置默认值
        /// </summary>
        private void SetDefaultValues()
        {
            try
            {
                var defaultConfig = new SimpleColorConfig();
                ApplyConfigToUI(defaultConfig);
                if (_parentPage != null)
                {
                    SyncToPage1Config(defaultConfig);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"设置默认值失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前配置到模板文件
        /// </summary>
        private void SaveCurrentSettings()
        {
            try
            {
                var config = GetCurrentConfigFromUI();
                SaveConfigToTemplate(config);
                if (_parentPage != null)
                {
                    SyncToPage1Config(config);
                }
                ApplyCurrentSettings();
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存颜色配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从界面获取当前配置
        /// </summary>
        private SimpleColorConfig GetCurrentConfigFromUI()
        {
            var config = new SimpleColorConfig();
            
            try
            {
                if (UseCustomColorRange != null) config.UseCustomColorRange = UseCustomColorRange.IsChecked ?? false;
                if (ColorRangeMinSlider != null) config.ColorRangeMin = ColorRangeMinSlider.Value;
                if (ColorRangeMaxSlider != null) config.ColorRangeMax = ColorRangeMaxSlider.Value;
                if (MeshTransparentSlider != null) config.MeshTransparent = MeshTransparentSlider.Value;
                if (BlendWeightSlider != null) config.BlendWeight = BlendWeightSlider.Value;
                if (DisplayColorBar != null) config.DisplayColorBar = DisplayColorBar.IsChecked ?? true;
                if (DisplayGrid != null) config.DisplayGrid = DisplayGrid.IsChecked ?? true;
                if (DisplayAxis != null) config.DisplayAxis = DisplayAxis.IsChecked ?? true;
            }
            catch (Exception ex)
            {
                LogManager.Error($"从界面获取配置失败: {ex.Message}");
            }
            
            return config;
        }

        /// <summary>
        /// 保存配置到模板文件
        /// </summary>
        private void SaveConfigToTemplate(SimpleColorConfig config)
        {
            try
            {
                string templatePath = GetCurrentTemplateFilePath();
                if (string.IsNullOrEmpty(templatePath))
                {
                    LogManager.Warning("无法获取模板文件路径，跳过保存");
                    return;
                }

                // 如果模板文件不存在，创建默认模板
                TemplateParameters template;
                if (File.Exists(templatePath))
                {
                    template = TemplateParameters.LoadFromFile(templatePath);
                }
                else
                {
                    template = new TemplateParameters
                    {
                        TemplateName = GetCurrentTemplateName(),
                        CreatedTime = DateTime.Now,
                        LastModifiedTime = DateTime.Now
                    };
                    
                    // 确保目录存在
                    string directory = Path.GetDirectoryName(templatePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }

                // 更新颜色配置
                template.ColorParams.UseCustomColorRange = config.UseCustomColorRange;
                template.ColorParams.ColorRangeMin = config.ColorRangeMin;
                template.ColorParams.ColorRangeMax = config.ColorRangeMax;
                template.ColorParams.MeshTransparent = (float)config.MeshTransparent;
                template.ColorParams.BlendWeight = (float)config.BlendWeight;
                template.ColorParams.DisplayColorBar = config.DisplayColorBar;
                template.ColorParams.DisplayGrid = config.DisplayGrid;
                template.ColorParams.DisplayAxis = config.DisplayAxis;

                // 保存模板文件
                template.SaveToFile(templatePath);
                LogManager.Info($"颜色配置已保存到模板: {GetCurrentTemplateName()}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存颜色配置到模板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 同步配置到Page1的配置对象
        /// </summary>
        private void SyncToPage1Config(SimpleColorConfig config)
        {
            try
            {
                if (_parentPage?._3DColorConfig != null)
                {
                    _parentPage._3DColorConfig.UseCustomColorRange = config.UseCustomColorRange;
                    _parentPage._3DColorConfig.ColorRangeMin = config.ColorRangeMin;
                    _parentPage._3DColorConfig.ColorRangeMax = config.ColorRangeMax;
                    _parentPage._3DColorConfig.MeshTransparent = (float)config.MeshTransparent;
                    _parentPage._3DColorConfig.BlendWeight = (float)config.BlendWeight;
                    _parentPage._3DColorConfig.DisplayColorBar = config.DisplayColorBar;
                    _parentPage._3DColorConfig.DisplayGrid = config.DisplayGrid;
                    _parentPage._3DColorConfig.DisplayAxis = config.DisplayAxis;
                }

                if (_parentPage?._2DColorConfig != null)
                {
                    _parentPage._2DColorConfig.UseCustomColorRange = config.UseCustomColorRange;
                    _parentPage._2DColorConfig.ColorRangeMin = config.ColorRangeMin;
                    _parentPage._2DColorConfig.ColorRangeMax = config.ColorRangeMax;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"同步配置到Page1失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新界面显示值
        /// </summary>
        private void UpdateDisplayValues()
        {
            try
            {
                if (ColorRangeMinValue != null && ColorRangeMinSlider != null)
                    ColorRangeMinValue.Text = ColorRangeMinSlider.Value.ToString("F3");
                if (ColorRangeMaxValue != null && ColorRangeMaxSlider != null)
                    ColorRangeMaxValue.Text = ColorRangeMaxSlider.Value.ToString("F3");
                if (MeshTransparentValue != null && MeshTransparentSlider != null)
                    MeshTransparentValue.Text = MeshTransparentSlider.Value.ToString("F1");
                if (BlendWeightValue != null && BlendWeightSlider != null)
                    BlendWeightValue.Text = BlendWeightSlider.Value.ToString("F1");
                if (CurrentRangeDisplay != null && ColorRangeMinSlider != null && ColorRangeMaxSlider != null)
                    CurrentRangeDisplay.Text = $"{ColorRangeMinSlider.Value:F3}, {ColorRangeMaxSlider.Value:F3}";
                
                // 同步更新TextBox
                if (ColorRangeMinInput != null && ColorRangeMinSlider != null)
                    ColorRangeMinInput.Text = ColorRangeMinSlider.Value.ToString("F3");
                if (ColorRangeMaxInput != null && ColorRangeMaxSlider != null)
                    ColorRangeMaxInput.Text = ColorRangeMaxSlider.Value.ToString("F3");
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新显示值失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用当前设置到3D视图（通过Page1的公共方法）
        /// </summary>
        private void ApplyCurrentSettings()
        {
            try
            {
                if (_parentPage != null)
                {
                    var config = GetCurrentConfigFromUI();
                    
                    // 调用Page1的公共方法来应用配置
                    _parentPage.ApplyColorConfigFromWindow(
                        config.UseCustomColorRange,
                        config.ColorRangeMin,
                        config.ColorRangeMax,
                        config.MeshTransparent,
                        config.BlendWeight,
                        config.DisplayColorBar,
                        config.DisplayGrid,
                        config.DisplayAxis
                    );
                    
                    LogManager.Info($"颜色配置已通过Page1应用: 自定义={config.UseCustomColorRange}, 范围=[{config.ColorRangeMin:F3}, {config.ColorRangeMax:F3}]");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用当前设置失败: {ex.Message}");
            }
        }

        // 事件处理器
        private void UseCustomColorRange_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                if (RangeSettings != null) RangeSettings.IsEnabled = UseCustomColorRange?.IsChecked ?? false;
                SaveCurrentSettings();
            }
            catch (Exception ex)
            {
                LogManager.Error($"自定义颜色范围开关事件处理失败: {ex.Message}");
            }
        }

        private void ColorRangeMinSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            try
            {
                if (ColorRangeMaxSlider != null && e.NewValue >= ColorRangeMaxSlider.Value)
                {
                    ColorRangeMinSlider.Value = ColorRangeMaxSlider.Value - 0.001;
                    return;
                }
                UpdateDisplayValues();
            }
            catch (Exception ex)
            {
                LogManager.Error($"最小值滑块事件处理失败: {ex.Message}");
            }
        }

        private void ColorRangeMaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            try
            {
                if (ColorRangeMinSlider != null && e.NewValue <= ColorRangeMinSlider.Value)
                {
                    ColorRangeMaxSlider.Value = ColorRangeMinSlider.Value + 0.001;
                    return;
                }
                UpdateDisplayValues();
            }
            catch (Exception ex)
            {
                LogManager.Error($"最大值滑块事件处理失败: {ex.Message}");
            }
        }

        private void MeshTransparentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            try
            {
                UpdateDisplayValues();
            }
            catch (Exception ex)
            {
                LogManager.Error($"网格透明度滑块事件处理失败: {ex.Message}");
            }
        }

        private void BlendWeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            try
            {
                UpdateDisplayValues();
            }
            catch (Exception ex)
            {
                LogManager.Error($"混合权重滑块事件处理失败: {ex.Message}");
            }
        }

        private void ColorRangeSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SaveCurrentSettings();
            }
            catch (Exception ex)
            {
                LogManager.Error($"颜色范围滑块松开事件处理失败: {ex.Message}");
            }
        }

        private void DisplayOption_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SaveCurrentSettings();
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示选项松开事件处理失败: {ex.Message}");
            }
        }

        private void DisplayOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SaveCurrentSettings();
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示选项变化事件处理失败: {ex.Message}");
            }
        }

        // TextBox事件处理器
        private void ColorRangeMinInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ApplyMinInputValue();
            }
        }

        private void ColorRangeMinInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyMinInputValue();
        }

        private void ColorRangeMaxInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ApplyMaxInputValue();
            }
        }

        private void ColorRangeMaxInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyMaxInputValue();
        }

        private void ApplyMinInputValue()
        {
            if (_isLoading) return;
            try
            {
                if (ColorRangeMinInput != null && ColorRangeMinSlider != null)
                {
                    if (double.TryParse(ColorRangeMinInput.Text, out double value))
                    {
                        // 限制在有效范围内
                        value = Math.Max(-1.5, Math.Min(1.5, value));
                        
                        // 确保小于最大值
                        if (ColorRangeMaxSlider != null && value >= ColorRangeMaxSlider.Value)
                        {
                            value = ColorRangeMaxSlider.Value - 0.001;
                        }
                        
                        // 临时设置标志避免递归
                        _isLoading = true;
                        ColorRangeMinSlider.Value = value;
                        _isLoading = false;
                        
                        // 手动更新其他UI元素
                        if (ColorRangeMinValue != null)
                            ColorRangeMinValue.Text = value.ToString("F3");
                        if (CurrentRangeDisplay != null && ColorRangeMaxSlider != null)
                            CurrentRangeDisplay.Text = $"{value:F3}, {ColorRangeMaxSlider.Value:F3}";
                        
                        SaveCurrentSettings();
                    }
                    else
                    {
                        // 恢复到当前滑块值
                        ColorRangeMinInput.Text = ColorRangeMinSlider.Value.ToString("F3");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用最小值输入失败: {ex.Message}");
            }
        }

        private void ApplyMaxInputValue()
        {
            if (_isLoading) return;
            try
            {
                if (ColorRangeMaxInput != null && ColorRangeMaxSlider != null)
                {
                    if (double.TryParse(ColorRangeMaxInput.Text, out double value))
                    {
                        // 限制在有效范围内
                        value = Math.Max(-1.5, Math.Min(1.5, value));
                        
                        // 确保大于最小值
                        if (ColorRangeMinSlider != null && value <= ColorRangeMinSlider.Value)
                        {
                            value = ColorRangeMinSlider.Value + 0.001;
                        }
                        
                        // 临时设置标志避免递归
                        _isLoading = true;
                        ColorRangeMaxSlider.Value = value;
                        _isLoading = false;
                        
                        // 手动更新其他UI元素
                        if (ColorRangeMaxValue != null)
                            ColorRangeMaxValue.Text = value.ToString("F3");
                        if (CurrentRangeDisplay != null && ColorRangeMinSlider != null)
                            CurrentRangeDisplay.Text = $"{ColorRangeMinSlider.Value:F3}, {value:F3}";
                        
                        SaveCurrentSettings();
                    }
                    else
                    {
                        // 恢复到当前滑块值
                        ColorRangeMaxInput.Text = ColorRangeMaxSlider.Value.ToString("F3");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用最大值输入失败: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                LogManager.Info("颜色配置窗口已关闭");
                base.OnClosed(e);
            }
            catch (Exception ex)
            {
                LogManager.Error($"关闭颜色配置窗口失败: {ex.Message}");
            }
        }
    }
} 