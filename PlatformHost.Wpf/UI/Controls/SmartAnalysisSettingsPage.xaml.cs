using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// 智能分析告警设置页面
    /// </summary>
    public partial class SmartAnalysisSettingsPage : UserControl
    {
        public event EventHandler SettingsSaved;
        public event EventHandler SettingsCancelled;

        private AlertSettings _editingSettings;
        private ObservableCollection<AlertStrategyProfile> _profileCollection = new ObservableCollection<AlertStrategyProfile>();
        private ObservableCollection<ItemProfileAssignment> _itemAssignments = new ObservableCollection<ItemProfileAssignment>();
        private ObservableCollection<ProfileOption> _profileOptions = new ObservableCollection<ProfileOption>();
        private AlertStrategyProfile _selectedProfile;
        private bool _suppressSelectionChanged;

        public IEnumerable<ProfileOption> ProfileOptions => _profileOptions;

        public SmartAnalysisSettingsPage()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            _suppressSelectionChanged = true;
            try
            {
                _editingSettings = AlertSettings.Load().Clone();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载告警设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _editingSettings = new AlertSettings();
            }

            _profileCollection = new ObservableCollection<AlertStrategyProfile>(_editingSettings.StrategyProfiles ?? new List<AlertStrategyProfile>());
            if (_profileCollection.Count == 0)
            {
                var defaultProfile = AlertStrategyProfile.CreateDefault("默认策略组合");
                defaultProfile.IsDefault = true;
                _profileCollection.Add(defaultProfile);
                _editingSettings.StrategyProfiles = _profileCollection.ToList();
                _editingSettings.DefaultProfileId = defaultProfile.Id;
            }

            ProfileListBox.ItemsSource = _profileCollection;

            EnableAlertCheckBox.IsChecked = _editingSettings.IsEnabled;
            StatisticsCycleTextBox.Text = _editingSettings.StatisticsCycle.ToString();
            MinSampleSizeTextBox.Text = _editingSettings.MinSampleSize.ToString();

            RefreshProfileOptions();
            BuildItemAssignments();

            if (_profileCollection.Count > 0)
            {
                ProfileListBox.SelectedIndex = 0;
            }

            _suppressSelectionChanged = false;
            DisplaySelectedProfile();
        }

        private void BuildItemAssignments()
        {
            _itemAssignments.Clear();

            var availableItems = new HashSet<string>(SmartAnalysisMainPage.GetAllAvailableItemNames() ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var existing in _editingSettings.ItemProfileBindings.Keys)
            {
                availableItems.Add(existing);
            }

            foreach (var itemName in availableItems.OrderBy(name => name))
            {
                var assignedId = _editingSettings.ItemProfileBindings.TryGetValue(itemName, out var profileId)
                    ? profileId
                    : string.Empty;
                _itemAssignments.Add(new ItemProfileAssignment(itemName, assignedId, OnItemProfileChanged));
            }

            ItemAssignmentListView.ItemsSource = _itemAssignments;
        }

        private void OnItemProfileChanged(string itemName, string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                _editingSettings.ItemProfileBindings.Remove(itemName);
            }
            else
            {
                _editingSettings.ItemProfileBindings[itemName] = profileId;
            }
        }

        private void RefreshProfileOptions()
        {
            _profileOptions.Clear();
            _profileOptions.Add(new ProfileOption(string.Empty, "跟随默认组合"));

            foreach (var profile in _profileCollection)
            {
                var label = profile.IsDefault ? $"{profile.Name}（默认）" : profile.Name;
                _profileOptions.Add(new ProfileOption(profile.Id, label));
            }

            foreach (var assignment in _itemAssignments)
            {
                if (!string.IsNullOrEmpty(assignment.AssignedProfileId) &&
                    !_profileCollection.Any(p => string.Equals(p.Id, assignment.AssignedProfileId, StringComparison.OrdinalIgnoreCase)))
                {
                    assignment.AssignedProfileId = string.Empty;
                }
            }

            ProfileListBox.Items.Refresh();
        }

        private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            if (!ApplyProfileDetailChanges(e.RemovedItems))
            {
                return;
            }

            _selectedProfile = ProfileListBox.SelectedItem as AlertStrategyProfile;
            DisplaySelectedProfile();
        }

        private bool ApplyProfileDetailChanges(IList removedItems = null)
        {
            if (_selectedProfile == null)
            {
                return true;
            }

            if (!ValidateAndApplyProfileInputs(_selectedProfile))
            {
                _suppressSelectionChanged = true;
                if (removedItems != null && removedItems.Count > 0)
                {
                    ProfileListBox.SelectedItem = removedItems[0];
                }
                else
                {
                    ProfileListBox.SelectedItem = _selectedProfile;
                }
                _suppressSelectionChanged = false;
                return false;
            }

            return true;
        }

        private void DisplaySelectedProfile()
        {
            _selectedProfile = ProfileListBox.SelectedItem as AlertStrategyProfile;
            if (_selectedProfile == null)
            {
                ProfileDetailsPanel.IsEnabled = false;
                ProfileNameTextBox.Text = string.Empty;
                EnableCountAnalysisCheckBox.IsChecked = false;
                OutOfRangeThresholdTextBox.Text = string.Empty;
                EnableProcessCapabilityCheckBox.IsChecked = false;
                CAThresholdTextBox.Text = string.Empty;
                CPThresholdTextBox.Text = string.Empty;
                CPKThresholdTextBox.Text = string.Empty;
                EnableConsecutiveNGCheckBox.IsChecked = false;
                ConsecutiveNGThresholdTextBox.Text = string.Empty;
                return;
            }

            ProfileDetailsPanel.IsEnabled = true;
            ProfileNameTextBox.Text = _selectedProfile.Name;
            EnableCountAnalysisCheckBox.IsChecked = _selectedProfile.EnableCountAnalysis;
            OutOfRangeThresholdTextBox.Text = _selectedProfile.OutOfRangeThreshold.ToString();
            EnableProcessCapabilityCheckBox.IsChecked = _selectedProfile.EnableProcessCapabilityAnalysis;
            CAThresholdTextBox.Text = _selectedProfile.CAThreshold.ToString("F3");
            CPThresholdTextBox.Text = _selectedProfile.CPThreshold.ToString("F3");
            CPKThresholdTextBox.Text = _selectedProfile.CPKThreshold.ToString("F3");
            EnableConsecutiveNGCheckBox.IsChecked = _selectedProfile.EnableConsecutiveNGAnalysis;
            ConsecutiveNGThresholdTextBox.Text = _selectedProfile.ConsecutiveNGThreshold.ToString();
        }

        private bool ValidateAndApplyProfileInputs(AlertStrategyProfile profile)
        {
            try
            {
                profile.EnableCountAnalysis = EnableCountAnalysisCheckBox.IsChecked == true;
                if (!int.TryParse(OutOfRangeThresholdTextBox.Text, out var outOfRange) || outOfRange <= 0)
                {
                    MessageBox.Show("超限次数阈值必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OutOfRangeThresholdTextBox.Focus();
                    OutOfRangeThresholdTextBox.SelectAll();
                    return false;
                }
                profile.OutOfRangeThreshold = outOfRange;

                profile.EnableProcessCapabilityAnalysis = EnableProcessCapabilityCheckBox.IsChecked == true;
                if (profile.EnableProcessCapabilityAnalysis)
                {
                    if (!double.TryParse(CAThresholdTextBox.Text, out var ca) || ca <= 0)
                    {
                        MessageBox.Show("CA阈值必须是大于0的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        CAThresholdTextBox.Focus();
                        CAThresholdTextBox.SelectAll();
                        return false;
                    }
                    if (!double.TryParse(CPThresholdTextBox.Text, out var cp) || cp <= 0)
                    {
                        MessageBox.Show("CP阈值必须是大于0的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        CPThresholdTextBox.Focus();
                        CPThresholdTextBox.SelectAll();
                        return false;
                    }
                    if (!double.TryParse(CPKThresholdTextBox.Text, out var cpk) || cpk <= 0)
                    {
                        MessageBox.Show("CPK阈值必须是大于0的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        CPKThresholdTextBox.Focus();
                        CPKThresholdTextBox.SelectAll();
                        return false;
                    }

                    profile.CAThreshold = ca;
                    profile.CPThreshold = cp;
                    profile.CPKThreshold = cpk;
                }
                else
                {
                    if (double.TryParse(CAThresholdTextBox.Text, out var caValue))
                    {
                        profile.CAThreshold = Math.Max(caValue, 0.01);
                    }
                    if (double.TryParse(CPThresholdTextBox.Text, out var cpValue))
                    {
                        profile.CPThreshold = Math.Max(cpValue, 0.01);
                    }
                    if (double.TryParse(CPKThresholdTextBox.Text, out var cpkValue))
                    {
                        profile.CPKThreshold = Math.Max(cpkValue, 0.01);
                    }
                }

                profile.EnableConsecutiveNGAnalysis = EnableConsecutiveNGCheckBox.IsChecked == true;
                if (!int.TryParse(ConsecutiveNGThresholdTextBox.Text, out var consecutive) || consecutive <= 0)
                {
                    MessageBox.Show("连续NG阈值必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ConsecutiveNGThresholdTextBox.Focus();
                    ConsecutiveNGThresholdTextBox.SelectAll();
                    return false;
                }
                profile.ConsecutiveNGThreshold = consecutive;

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新策略组合失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApplyProfileDetailChanges())
            {
                return;
            }

            var profile = AlertStrategyProfile.CreateDefault(GenerateProfileName());
            _profileCollection.Add(profile);
            _editingSettings.StrategyProfiles = _profileCollection.ToList();
            RefreshProfileOptions();
            ProfileListBox.SelectedItem = profile;
        }

        private void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null)
            {
                return;
            }

            if (!ApplyProfileDetailChanges())
            {
                return;
            }

            var clone = _selectedProfile.Clone();
            clone.Name = GenerateProfileName(_selectedProfile.Name);
            clone.IsDefault = false;
            _profileCollection.Add(clone);
            _editingSettings.StrategyProfiles = _profileCollection.ToList();
            RefreshProfileOptions();
            ProfileListBox.SelectedItem = clone;
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null)
            {
                return;
            }

            if (_profileCollection.Count <= 1)
            {
                MessageBox.Show("至少需要保留一个策略组合", "操作受限", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedProfile.IsDefault)
            {
                MessageBox.Show("无法删除默认组合，请先设置其他组合为默认", "操作受限", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要删除策略组合“{_selectedProfile.Name}”吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            var removedId = _selectedProfile.Id;
            _profileCollection.Remove(_selectedProfile);
            _editingSettings.StrategyProfiles = _profileCollection.ToList();

            foreach (var assignment in _itemAssignments)
            {
                if (string.Equals(assignment.AssignedProfileId, removedId, StringComparison.OrdinalIgnoreCase))
                {
                    assignment.AssignedProfileId = string.Empty;
                }
            }

            RefreshProfileOptions();
            ProfileListBox.SelectedIndex = 0;
        }

        private void SetDefaultProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null)
            {
                return;
            }

            if (!ApplyProfileDetailChanges())
            {
                return;
            }

            SetDefaultProfile(_selectedProfile);
        }

        private void SetDefaultProfile(AlertStrategyProfile profile)
        {
            _editingSettings.DefaultProfileId = profile.Id;
            foreach (var p in _profileCollection)
            {
                p.IsDefault = string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase);
            }
            RefreshProfileOptions();
        }

        private void ProfileNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitProfileNameChange();
        }

        private void ProfileNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitProfileNameChange();
                e.Handled = true;
            }
        }

        private void CommitProfileNameChange()
        {
            if (_selectedProfile == null)
            {
                return;
            }

            var newName = ProfileNameTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                ProfileNameTextBox.Text = _selectedProfile.Name;
                return;
            }

            if (_profileCollection.Any(p => !ReferenceEquals(p, _selectedProfile) &&
                                            string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("策略组合名称已存在，请使用其他名称", "命名冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfileNameTextBox.Text = _selectedProfile.Name;
                ProfileNameTextBox.Focus();
                ProfileNameTextBox.SelectAll();
                return;
            }

            _selectedProfile.Name = newName;
            RefreshProfileOptions();
        }

        private void RefreshItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApplyProfileDetailChanges())
            {
                return;
            }

            BuildItemAssignments();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApplyProfileDetailChanges())
            {
                return;
            }

            if (!int.TryParse(StatisticsCycleTextBox.Text, out var statisticsCycle) || statisticsCycle <= 0)
            {
                MessageBox.Show("统计周期必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatisticsCycleTextBox.Focus();
                StatisticsCycleTextBox.SelectAll();
                return;
            }

            if (!int.TryParse(MinSampleSizeTextBox.Text, out var minSampleSize) || minSampleSize <= 0)
            {
                MessageBox.Show("最小样本量必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                MinSampleSizeTextBox.Focus();
                MinSampleSizeTextBox.SelectAll();
                return;
            }

            foreach (var profile in _profileCollection)
            {
                if (profile.EnableCountAnalysis && profile.OutOfRangeThreshold <= 0)
                {
                    MessageBox.Show($"策略组合“{profile.Name}”的超限次数阈值必须大于0", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (profile.EnableProcessCapabilityAnalysis)
                {
                    if (profile.CAThreshold <= 0 || profile.CPThreshold <= 0 || profile.CPKThreshold <= 0)
                    {
                        MessageBox.Show($"策略组合“{profile.Name}”的过程能力阈值必须大于0", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (profile.EnableConsecutiveNGAnalysis && profile.ConsecutiveNGThreshold <= 0)
                {
                    MessageBox.Show($"策略组合“{profile.Name}”的连续NG阈值必须大于0", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (EnableAlertCheckBox.IsChecked == true && !_profileCollection.Any(p => p.HasAnyStrategyEnabled))
            {
                MessageBox.Show("启用告警功能时至少需要一个策略组合开启告警策略", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingSettings.IsEnabled = EnableAlertCheckBox.IsChecked == true;
            _editingSettings.StatisticsCycle = statisticsCycle;
            _editingSettings.MinSampleSize = minSampleSize;
            _editingSettings.StrategyProfiles = _profileCollection.ToList();
            _editingSettings.DefaultProfileId = _profileCollection.FirstOrDefault(p => p.IsDefault)?.Id ?? _profileCollection.First().Id;

            _editingSettings.ItemProfileBindings = _itemAssignments
                .Where(a => !string.IsNullOrWhiteSpace(a.AssignedProfileId))
                .ToDictionary(a => a.ItemName, a => a.AssignedProfileId, StringComparer.OrdinalIgnoreCase);

            _editingSettings.EnsureProfileConsistency();

            try
            {
                _editingSettings.Save();
                MessageBox.Show("告警设置已保存", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
            SettingsCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApplyProfileDetailChanges())
            {
                return;
            }

            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show("设置已修改，是否放弃更改？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            LoadCurrentSettings();
            SettingsCancelled?.Invoke(this, EventArgs.Empty);
        }

        public AlertSettings GetCurrentAlertSettings()
        {
            ApplyProfileDetailChanges();
            _editingSettings.StrategyProfiles = _profileCollection.ToList();
            _editingSettings.DefaultProfileId = _profileCollection.FirstOrDefault(p => p.IsDefault)?.Id ?? _profileCollection.First().Id;
            _editingSettings.ItemProfileBindings = _itemAssignments
                .Where(a => !string.IsNullOrWhiteSpace(a.AssignedProfileId))
                .ToDictionary(a => a.ItemName, a => a.AssignedProfileId, StringComparer.OrdinalIgnoreCase);
            _editingSettings.EnsureProfileConsistency();
            return _editingSettings.Clone();
        }

        private bool HasUnsavedChanges()
        {
            try
            {
                var saved = AlertSettings.Load();
                saved.EnsureProfileConsistency();

                _editingSettings.StrategyProfiles = _profileCollection.ToList();
                _editingSettings.ItemProfileBindings = _itemAssignments
                    .Where(a => !string.IsNullOrWhiteSpace(a.AssignedProfileId))
                    .ToDictionary(a => a.ItemName, a => a.AssignedProfileId, StringComparer.OrdinalIgnoreCase);
                _editingSettings.EnsureProfileConsistency();

                if (saved.IsEnabled != _editingSettings.IsEnabled ||
                    saved.StatisticsCycle != _editingSettings.StatisticsCycle ||
                    saved.MinSampleSize != _editingSettings.MinSampleSize ||
                    !string.Equals(saved.DefaultProfileId, _editingSettings.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (saved.StrategyProfiles.Count != _profileCollection.Count)
                {
                    return true;
                }

                foreach (var profile in _profileCollection)
                {
                    var savedProfile = saved.StrategyProfiles.FirstOrDefault(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
                    if (savedProfile == null)
                    {
                        return true;
                    }

                    if (!string.Equals(profile.Name, savedProfile.Name, StringComparison.OrdinalIgnoreCase) ||
                        profile.EnableCountAnalysis != savedProfile.EnableCountAnalysis ||
                        profile.OutOfRangeThreshold != savedProfile.OutOfRangeThreshold ||
                        profile.EnableProcessCapabilityAnalysis != savedProfile.EnableProcessCapabilityAnalysis ||
                        Math.Abs(profile.CAThreshold - savedProfile.CAThreshold) > 0.0001 ||
                        Math.Abs(profile.CPThreshold - savedProfile.CPThreshold) > 0.0001 ||
                        Math.Abs(profile.CPKThreshold - savedProfile.CPKThreshold) > 0.0001 ||
                        profile.EnableConsecutiveNGAnalysis != savedProfile.EnableConsecutiveNGAnalysis ||
                        profile.ConsecutiveNGThreshold != savedProfile.ConsecutiveNGThreshold)
                    {
                        return true;
                    }
                }

                if (saved.ItemProfileBindings.Count != _editingSettings.ItemProfileBindings.Count)
                {
                    return true;
                }

                foreach (var kv in _editingSettings.ItemProfileBindings)
                {
                    if (!saved.ItemProfileBindings.TryGetValue(kv.Key, out var savedValue) ||
                        !string.Equals(savedValue, kv.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        private string GenerateProfileName(string baseName = "策略组合")
        {
            var existingNames = new HashSet<string>(_profileCollection.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "策略组合";
            }

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            var index = 2;
            while (true)
            {
                var candidate = $"{baseName} {index}";
                if (!existingNames.Contains(candidate))
                {
                    return candidate;
                }
                index++;
            }
        }

        public class ProfileOption
        {
            public ProfileOption(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }
            public string DisplayName { get; }
        }

        private class ItemProfileAssignment : INotifyPropertyChanged
        {
            private readonly Action<string, string> _onProfileChanged;
            private string _assignedProfileId;

            public ItemProfileAssignment(string itemName, string profileId, Action<string, string> onProfileChanged)
            {
                ItemName = itemName;
                _assignedProfileId = profileId ?? string.Empty;
                _onProfileChanged = onProfileChanged;
            }

            public string ItemName { get; }

            public string AssignedProfileId
            {
                get => _assignedProfileId;
                set
                {
                    var normalized = value ?? string.Empty;
                    if (!string.Equals(_assignedProfileId, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        _assignedProfileId = normalized;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AssignedProfileId)));
                        _onProfileChanged?.Invoke(ItemName, normalized);
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
