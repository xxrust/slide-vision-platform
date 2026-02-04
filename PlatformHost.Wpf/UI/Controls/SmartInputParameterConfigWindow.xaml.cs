using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Collections.Generic;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// 智能输入参数配置窗口
    /// </summary>
    public partial class SmartInputParameterConfigWindow : Window
    {
        private string _parameterKey;
        private SmartInputParameterConfiguration _configuration;

        public SmartInputParameterConfigWindow()
        {
            InitializeComponent();
        }

        public SmartInputParameterConfigWindow(string stepName, string parameterName) : this()
        {
            _parameterKey = $"{stepName}_{parameterName}";
            ParameterNameTextBox.Text = $"{stepName} - {parameterName}";
            
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SmartInputConfigs.json");
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var configs = JsonSerializer.Deserialize<Dictionary<string, SmartInputParameterConfiguration>>(json) ?? 
                                 new Dictionary<string, SmartInputParameterConfiguration>();
                    
                    if (configs.ContainsKey(_parameterKey))
                    {
                        _configuration = configs[_parameterKey];
                    }
                    else
                    {
                        _configuration = new SmartInputParameterConfiguration();
                    }
                }
                else
                {
                    _configuration = new SmartInputParameterConfiguration();
                }

                // 加载到界面
                SetUnitComboBox(_configuration.Unit ?? "");
                MinValueTextBox.Text = _configuration.MinValue?.ToString() ?? "0";
                MaxValueTextBox.Text = _configuration.MaxValue?.ToString() ?? "100";
                SetPrecisionComboBox(_configuration.StepSize);
                ImagePathTextBox.Text = _configuration.ImagePath ?? "";
                DescriptionTextBox.Text = _configuration.Description ?? "";
            }
            catch (Exception ex)
            {
                LogManager.Warning($"加载参数配置失败: {ex.Message}");
                _configuration = new SmartInputParameterConfiguration();
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                // 验证输入
                if (!double.TryParse(MinValueTextBox.Text, out double minValue))
                {
                    MessageBox.Show("最小值格式不正确", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!double.TryParse(MaxValueTextBox.Text, out double maxValue))
                {
                    MessageBox.Show("最大值格式不正确", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (minValue >= maxValue)
                {
                    MessageBox.Show("最小值必须小于最大值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 更新配置
                _configuration.Unit = GetUnitFromComboBox();
                _configuration.MinValue = minValue;
                _configuration.MaxValue = maxValue;
                _configuration.StepSize = GetPrecisionFromComboBox();
                _configuration.ImagePath = ImagePathTextBox.Text.Trim();
                _configuration.Description = DescriptionTextBox.Text.Trim();

                // 保存到文件
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SmartInputConfigs.json");
                
                // 确保目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));

                Dictionary<string, SmartInputParameterConfiguration> allConfigs;
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    allConfigs = JsonSerializer.Deserialize<Dictionary<string, SmartInputParameterConfiguration>>(json) ?? 
                               new Dictionary<string, SmartInputParameterConfiguration>();
                }
                else
                {
                    allConfigs = new Dictionary<string, SmartInputParameterConfiguration>();
                }

                allConfigs[_parameterKey] = _configuration;

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                File.WriteAllText(configPath, JsonSerializer.Serialize(allConfigs, options));

                LogManager.Info($"参数配置已保存: {_parameterKey}");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Error($"保存参数配置失败: {ex.Message}");
            }
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "选择示例图片",
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    var selectedFile = dialog.FileName;
                    var resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ParameterImages");
                    
                    // 确保目录存在
                    Directory.CreateDirectory(resourcesDir);

                    // 生成新的文件名，避免冲突
                    var fileName = $"{_parameterKey}_{Path.GetFileName(selectedFile)}";
                    var targetPath = Path.Combine(resourcesDir, fileName);

                    // 复制文件
                    File.Copy(selectedFile, targetPath, true);
                    
                    // 只保存相对路径
                    ImagePathTextBox.Text = fileName;
                    
                    LogManager.Info($"图片已复制到: {targetPath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择图片失败: {ex.Message}", "文件错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Error($"选择图片失败: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public SmartInputParameterConfiguration GetConfiguration()
        {
            return _configuration;
        }

        /// <summary>
        /// 设置精度下拉框的值
        /// </summary>
        private void SetPrecisionComboBox(double stepSize)
        {
            var stepSizes = new[] { 1.0, 0.1, 0.01, 0.001};
            var index = Array.IndexOf(stepSizes, stepSize);
            PrecisionComboBox.SelectedIndex = index >= 0 ? index : 2; // 默认为0.01
        }

        /// <summary>
        /// 从精度下拉框获取步长值
        /// </summary>
        private double GetPrecisionFromComboBox()
        {
            if (PrecisionComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag != null)
            {
                if (double.TryParse(item.Tag.ToString(), out double stepSize))
                {
                    return stepSize;
                }
            }
            return 1.0; // 默认步长
        }

        /// <summary>
        /// 设置单位下拉框的值
        /// </summary>
        private void SetUnitComboBox(string unit)
        {
            var presetUnits = new[] { "um", "mm", "pix" };
            var unitIndex = Array.IndexOf(presetUnits, unit);
            
            if (string.IsNullOrEmpty(unit))
            {
                // 如果单位为空，选择"无单位"
                UnitComboBox.SelectedIndex = 0;
                CustomUnitTextBox.Visibility = Visibility.Collapsed;
            }
            else if (unitIndex >= 0)
            {
                UnitComboBox.SelectedIndex = unitIndex + 1; // 因为第一项是"无单位"，所以要+1
                CustomUnitTextBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                UnitComboBox.SelectedIndex = 4; // "其他"
                CustomUnitTextBox.Text = unit;
                CustomUnitTextBox.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 从单位下拉框获取单位值
        /// </summary>
        private string GetUnitFromComboBox()
        {
            if (UnitComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                var content = selectedItem.Content.ToString();
                
                if (content == "无单位")
                {
                    return "";
                }
                else if (content == "其他")
                {
                    return CustomUnitTextBox.Text.Trim();
                }
                return content;
            }
            return ""; // 默认值
        }

        /// <summary>
        /// 单位下拉框选择改变事件
        /// </summary>
        private void UnitComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CustomUnitTextBox != null)
            {
                if (UnitComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && selectedItem.Content.ToString() == "其他")
                {
                    CustomUnitTextBox.Visibility = Visibility.Visible;
                }
                else
                {
                    CustomUnitTextBox.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    /// <summary>
    /// 智能输入参数配置类
    /// </summary>
    public class SmartInputParameterConfiguration
    {
        public string Unit { get; set; } = "";
        public double? MinValue { get; set; } = 0;
        public double? MaxValue { get; set; } = 100;
        public double StepSize { get; set; } = 1.0;
        public string ImagePath { get; set; } = "";
        public string Description { get; set; } = "";
    }
} 