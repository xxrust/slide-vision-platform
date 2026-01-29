using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class Page1
    {
        private const string CicdFolderName = "CICD检测";
        private const string CicdTestFolderName = "测试";
        private const string CicdReferCsvName = "cicd_refer.csv";

        private bool _isCicdMode;
        private CicdRunContext _cicdRunContext;
        private CicdCompareDetailsWindow _cicdCompareDetailsWindow;

        private sealed class CicdRunContext
        {
            public bool IsBaselineRun { get; set; }
            public string TemplateName { get; set; }
            public string ImageSetName { get; set; }
            public string ImageSetFolderPath { get; set; }
            public List<CicdGroupSource> Groups { get; set; } = new List<CicdGroupSource>();
        }

        private sealed class CicdGroupSource
        {
            public string GroupName { get; set; }
            public string SourceRoot { get; set; }
            public List<string> SelectedSource1Files { get; set; }
        }

        private async Task StartCicdImageSetCollectionMode(List<string> selectedFiles)
        {
            try
            {
                if (selectedFiles == null || selectedFiles.Count == 0)
                {
                    LogUpdate("CICD图片集制作失败：未选择文件");
                    return;
                }

                foreach (var file in selectedFiles)
                {
                    if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                    {
                        LogUpdate($"CICD图片集制作失败：无效文件 {file}");
                        MessageBox.Show($"无效文件:\n{file}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                var loadingDialog = new LoadingDialog("正在准备CICD图片集测试，请稍候...");
                loadingDialog.Owner = Application.Current.MainWindow;
                loadingDialog.Show();
                await Task.Delay(100);

                try
                {
                    var groups = await Task.Run(() => DiscoverCicdGroupSourcesFromFiles(selectedFiles));
                    if (groups.Count == 0)
                    {
                        LogUpdate("CICD图片集制作失败：未找到任何图片组（请确认包含“图像源1”等目录）");
                        MessageBox.Show("未找到任何图片组（请确认包含“图像源1”等目录）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var allImageGroups = new List<ImageGroupSet>();
                    foreach (var group in groups)
                    {
                        if (group.SelectedSource1Files != null && group.SelectedSource1Files.Count > 0)
                        {
                            allImageGroups.AddRange(LoadCicdImageGroupsFromSelectedSource1Files(group.SourceRoot, group.GroupName, group.SelectedSource1Files));
                        }
                        else
                        {
                            allImageGroups.AddRange(LoadCicdImageGroupsFromGroupFolder(group.SourceRoot, group.GroupName));
                        }
                    }

                    if (allImageGroups.Count == 0)
                    {
                        LogUpdate("CICD图片集制作失败：未找到可用图片组（图片配对失败或文件结构不完整）");
                        MessageBox.Show("未找到可用图片组（图片配对失败或文件结构不完整）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    _isCicdMode = true;
                    _cicdRunContext = new CicdRunContext
                    {
                        IsBaselineRun = true,
                        TemplateName = CurrentTemplateName,
                        Groups = groups
                    };

                    if (!_isTestModeActive)
                    {
                        _testModeDataManager = new TestModeDataManager();
                        _testModeDataManager.StartTestMode();
                        _isTestModeActive = true;
                        LogManager.Info("[CICD] 已启动测试模式（用于记录基准数据）");
                    }
                    else
                    {
                        if (_testModeDataManager != null)
                        {
                            _testModeDataManager.TestResults.Clear();
                            _testModeDataManager.MarkedImages.Clear();
                            LogManager.Info("[CICD] 已清空历史测试结果（开始新的基准采集）");
                        }
                    }

                    _imageTestManager.SetImageGroups(allImageGroups);
                    _imageTestManager.MoveToFirst();
                    _imageTestManager.SetState(ImageTestState.Testing);
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.ToLast);

                    UpdateImageTestCardUI();
                    LogUpdate($"CICD图片集制作：开始自动测试，共 {allImageGroups.Count} 组图片");
                    await ExecuteCurrentImageGroup();
                }
                finally
                {
                    await Task.Delay(200);
                    loadingDialog.Close();
                }
            }
            catch (Exception ex)
            {
                _isCicdMode = false;
                _cicdRunContext = null;
                LogUpdate($"CICD图片集制作失败: {ex.Message}");
                MessageBox.Show($"CICD图片集制作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartCicdImageSetTestMode(string imageSetName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageSetName))
                {
                    LogUpdate("CICD图片集测试失败：未选择图片集");
                    return;
                }

                string setFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "templates",
                    CurrentTemplateName,
                    CicdFolderName,
                    imageSetName);

                string referCsvPath = Path.Combine(setFolder, CicdReferCsvName);
                if (!Directory.Exists(setFolder) || !File.Exists(referCsvPath))
                {
                    LogUpdate("CICD图片集测试失败：未找到基准文件 cicd_refer.csv");
                    MessageBox.Show($"未找到图片集或基准文件:\n{setFolder}\n\n请先使用「CICD图片集制作」生成基准。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var loadingDialog = new LoadingDialog("正在加载CICD图片集，请稍候...");
                loadingDialog.Owner = Application.Current.MainWindow;
                loadingDialog.Show();
                await Task.Delay(100);

                try
                {
                    var groupFolders = Directory.GetDirectories(setFolder)
                        .Where(d => !string.Equals(Path.GetFileName(d), CicdTestFolderName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (groupFolders.Count == 0)
                    {
                        LogUpdate("CICD图片集测试失败：图片集内没有图片组文件夹");
                        MessageBox.Show("图片集内没有图片组文件夹", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var allImageGroups = new List<ImageGroupSet>();
                    foreach (var groupFolder in groupFolders)
                    {
                        string groupName = Path.GetFileName(groupFolder);
                        allImageGroups.AddRange(LoadCicdImageGroupsFromGroupFolder(groupFolder, groupName));
                    }

                    if (allImageGroups.Count == 0)
                    {
                        LogUpdate("CICD图片集测试失败：未找到可用图片组（图片配对失败或文件结构不完整）");
                        MessageBox.Show("未找到可用图片组（图片配对失败或文件结构不完整）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    _isCicdMode = true;
                    _cicdRunContext = new CicdRunContext
                    {
                        IsBaselineRun = false,
                        TemplateName = CurrentTemplateName,
                        ImageSetName = imageSetName,
                        ImageSetFolderPath = setFolder
                    };

                    if (!_isTestModeActive)
                    {
                        _testModeDataManager = new TestModeDataManager();
                        _testModeDataManager.StartTestMode();
                        _isTestModeActive = true;
                        LogManager.Info("[CICD] 已启动测试模式（用于记录对比数据）");
                    }
                    else
                    {
                        if (_testModeDataManager != null)
                        {
                            _testModeDataManager.TestResults.Clear();
                            _testModeDataManager.MarkedImages.Clear();
                            LogManager.Info("[CICD] 已清空历史测试结果（开始新的对比测试）");
                        }
                    }

                    _imageTestManager.SetImageGroups(allImageGroups);
                    _imageTestManager.MoveToFirst();
                    _imageTestManager.SetState(ImageTestState.Testing);
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.ToLast);

                    UpdateImageTestCardUI();
                    LogUpdate($"CICD图片集测试：开始自动测试，共 {allImageGroups.Count} 组图片");
                    await ExecuteCurrentImageGroup();
                }
                finally
                {
                    await Task.Delay(200);
                    loadingDialog.Close();
                }
            }
            catch (Exception ex)
            {
                _isCicdMode = false;
                _cicdRunContext = null;
                LogUpdate($"CICD图片集测试失败: {ex.Message}");
                MessageBox.Show($"CICD图片集测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task HandleCicdRunCompletedAsync()
        {
            try
            {
                if (!_isCicdMode || _cicdRunContext == null)
                {
                    return;
                }

                var results = _testModeDataManager?.GetAllResults() ?? new List<TestModeDetectionResult>();
                if (results.Count == 0)
                {
                    LogUpdate("CICD：未记录到任何测试结果");
                    return;
                }

                if (_cicdRunContext.IsBaselineRun)
                {
                    await HandleCicdBaselineCompletedAsync(results, _cicdRunContext);
                }
                else
                {
                    await HandleCicdTestCompletedAsync(results, _cicdRunContext);
                }
            }
            finally
            {
                _isCicdMode = false;
                _cicdRunContext = null;
                EndTestMode();
                ExitImageTestingAfterCicd();
            }
        }

        private void ExitImageTestingAfterCicd()
        {
            try
            {
                _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                _imageTestManager.SetImageGroups(new List<ImageGroupSet>());
                _imageTestManager.SetState(ImageTestState.Idle);
                UpdateImageTestCardUI();
                LogUpdate("CICD完成：已退出图片检测模式");
            }
            catch (Exception ex)
            {
                LogManager.Error($"CICD完成后退出图片检测模式失败: {ex.Message}");
            }
        }

        private async Task HandleCicdBaselineCompletedAsync(List<TestModeDetectionResult> results, CicdRunContext context)
        {
            string setName = PromptForTextSimple("CICD图片集名称", "请输入本次生成的CICD图片集名称：", DateTime.Now.ToString("yyyyMMdd"));
            if (string.IsNullOrWhiteSpace(setName))
            {
                MessageBox.Show("已取消生成CICD图片集（未输入名称）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            setName = SanitizeFolderName(setName);
            if (string.IsNullOrWhiteSpace(setName))
            {
                MessageBox.Show("图片集名称无效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string cicdRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", context.TemplateName, CicdFolderName);
            string setFolder = Path.Combine(cicdRoot, setName);
            string referCsv = Path.Combine(setFolder, CicdReferCsvName);

            if (Directory.Exists(setFolder))
            {
                var overwrite = MessageBox.Show(
                    $"图片集目录已存在，是否覆盖？\n\n{setFolder}",
                    "确认覆盖",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (overwrite != MessageBoxResult.Yes)
                {
                    MessageBox.Show("已取消生成CICD图片集（未覆盖原目录）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            var loadingDialog = new LoadingDialog("正在生成CICD图片集，请稍候...");
            loadingDialog.Owner = Application.Current.MainWindow;
            loadingDialog.Show();
            await Task.Delay(100);

            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(cicdRoot);

                    if (Directory.Exists(setFolder))
                    {
                        Directory.Delete(setFolder, true);
                    }

                    Directory.CreateDirectory(setFolder);

                    foreach (var group in context.Groups ?? new List<CicdGroupSource>())
                    {
                        if (string.IsNullOrWhiteSpace(group?.GroupName) || string.IsNullOrWhiteSpace(group.SourceRoot) || !Directory.Exists(group.SourceRoot))
                        {
                            continue;
                        }

                        string targetGroupFolder = Path.Combine(setFolder, SanitizeFolderName(group.GroupName));
                        if (group.SelectedSource1Files != null && group.SelectedSource1Files.Count > 0)
                        {
                            CopyCicdGroupFolderSelected(group.SourceRoot, targetGroupFolder, group.SelectedSource1Files);
                        }
                        else
                        {
                            CopyCicdGroupFolderAll(group.SourceRoot, targetGroupFolder);
                        }
                    }

                    WriteCicdCsv(referCsv, results, context.Groups);
                });
            }
            finally
            {
                await Task.Delay(200);
                loadingDialog.Close();
            }

            LogUpdate($"CICD图片集制作完成：{setName}");
            MessageBox.Show(
                $"CICD图片集制作完成。\n\n已生成:\n{setFolder}\n\n基准文件:\n{referCsv}",
                "制作完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async Task HandleCicdTestCompletedAsync(List<TestModeDetectionResult> results, CicdRunContext context)
        {
            string setName = context.ImageSetName ?? "";
            string setFolder = context.ImageSetFolderPath ?? "";
            if (string.IsNullOrWhiteSpace(setName) || string.IsNullOrWhiteSpace(setFolder))
            {
                return;
            }

            string referCsvPath = Path.Combine(setFolder, CicdReferCsvName);
            if (!File.Exists(referCsvPath))
            {
                MessageBox.Show($"未找到基准文件:\n{referCsvPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string outputDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "templates",
                context.TemplateName,
                CicdFolderName,
                CicdTestFolderName,
                setName);
            Directory.CreateDirectory(outputDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string testCsvPath = Path.Combine(outputDir, $"{timestamp}.csv");
            string reportPath = Path.Combine(outputDir, $"{timestamp}_compare.txt");
            bool isPass = false;
            var limitMap = BuildCicdItemLimitMap(results);

            var loadingDialog = new LoadingDialog("正在保存并对比CICD测试结果，请稍候...");
            loadingDialog.Owner = Application.Current.MainWindow;
            loadingDialog.Show();
            await Task.Delay(100);

            try
            {
                await Task.Run(() =>
                {
                    WriteCicdCsv(testCsvPath, results);
                    var compareReport = CompareCicdCsvFiles(referCsvPath, testCsvPath, context.TemplateName, out isPass);
                    File.WriteAllText(reportPath, compareReport, Encoding.UTF8);
                });
            }
            finally
            {
                await Task.Delay(200);
                loadingDialog.Close();
            }

            LogUpdate(isPass ? "CICD对比完成：通过" : "CICD对比完成：未通过");
            ShowCicdCompareDetailsWindow(context.TemplateName, referCsvPath, testCsvPath, limitMap);
            MessageBox.Show(
                (isPass ? "CICD测试通过。\n\n" : "CICD测试未通过。\n\n") +
                $"输出:\n{testCsvPath}\n\n对比报告:\n{reportPath}",
                "CICD对比结果",
                MessageBoxButton.OK,
                isPass ? MessageBoxImage.Information : MessageBoxImage.Warning);

            await Task.CompletedTask;
        }

        private Dictionary<string, CicdItemLimitInfo> BuildCicdItemLimitMap(List<TestModeDetectionResult> results)
        {
            var map = new Dictionary<string, CicdItemLimitInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in results ?? new List<TestModeDetectionResult>())
            {
                foreach (var item in result?.DetectionItems ?? new List<DetectionItem>())
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Name))
                    {
                        continue;
                    }

                    string name = item.Name.Trim();
                    if (map.ContainsKey(name))
                    {
                        continue;
                    }

                    var info = new CicdItemLimitInfo
                    {
                        ItemName = name,
                        LowerLimit = item.LowerLimit ?? "",
                        UpperLimit = item.UpperLimit ?? ""
                    };

                    if (info.HasAnyLimit)
                    {
                        map[name] = info;
                    }
                }
            }

            return map;
        }

        private void ShowCicdCompareDetailsWindow(
            string templateName,
            string referCsvPath,
            string testCsvPath,
            Dictionary<string, CicdItemLimitInfo> limitMap)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var standard = CicdAcceptanceCriteriaConfigManager.GetActiveStandard(templateName);

                    if (_cicdCompareDetailsWindow == null)
                    {
                        _cicdCompareDetailsWindow = new CicdCompareDetailsWindow();
                        _cicdCompareDetailsWindow.Owner = Window.GetWindow(this) ?? Application.Current?.MainWindow;
                        _cicdCompareDetailsWindow.Closed += (s, e) => { _cicdCompareDetailsWindow = null; };
                    }

                    _cicdCompareDetailsWindow.LoadComparison(
                        templateName,
                        referCsvPath,
                        testCsvPath,
                        standard,
                        limitMap);

                    if (!_cicdCompareDetailsWindow.IsVisible)
                    {
                        _cicdCompareDetailsWindow.Show();
                    }

                    _cicdCompareDetailsWindow.Activate();
                });
            }
            catch (Exception ex)
            {
                LogManager.Error($"打开CICD对比明细窗口失败: {ex}");
            }
        }

        private void ImportCicdTestCsvAndCompare()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "选择需要对比的测试CSV",
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    Multiselect = false,
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                string initDir = "";
                try
                {
                    if (!string.IsNullOrWhiteSpace(CurrentTemplateName))
                    {
                        initDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", CurrentTemplateName, CicdFolderName, CicdTestFolderName);
                    }
                }
                catch
                {
                    initDir = "";
                }

                if (!string.IsNullOrWhiteSpace(initDir) && Directory.Exists(initDir))
                {
                    dialog.InitialDirectory = initDir;
                }

                bool? ok = dialog.ShowDialog(Window.GetWindow(this) ?? Application.Current?.MainWindow);
                if (ok != true)
                {
                    return;
                }

                string testCsvPath = dialog.FileName;
                if (string.IsNullOrWhiteSpace(testCsvPath) || !File.Exists(testCsvPath))
                {
                    return;
                }

                string templateName;
                string referCsvPath;
                string cicdRoot;
                if (!TryResolveCicdReferenceCsvPath(testCsvPath, out templateName, out referCsvPath, out cicdRoot))
                {
                    templateName = string.IsNullOrWhiteSpace(CurrentTemplateName) ? "UnknownTemplate" : CurrentTemplateName;
                    referCsvPath = "";
                }

                if (string.IsNullOrWhiteSpace(referCsvPath) || !File.Exists(referCsvPath))
                {
                    var referDialog = new OpenFileDialog
                    {
                        Title = "选择参考CSV（cicd_refer.csv）",
                        Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                        Multiselect = false,
                        CheckFileExists = true,
                        CheckPathExists = true
                    };

                    if (!string.IsNullOrWhiteSpace(cicdRoot) && Directory.Exists(cicdRoot))
                    {
                        referDialog.InitialDirectory = cicdRoot;
                    }

                    bool? referOk = referDialog.ShowDialog(Window.GetWindow(this) ?? Application.Current?.MainWindow);
                    if (referOk != true)
                    {
                        return;
                    }

                    referCsvPath = referDialog.FileName;
                    if (string.IsNullOrWhiteSpace(referCsvPath) || !File.Exists(referCsvPath))
                    {
                        return;
                    }
                }

                var emptyLimits = new Dictionary<string, CicdItemLimitInfo>(StringComparer.OrdinalIgnoreCase);
                ShowCicdCompareDetailsWindow(templateName, referCsvPath, testCsvPath, emptyLimits);
                LogUpdate($"CICD CSV对比：已导入测试CSV并打开对比明细\n测试: {testCsvPath}\n参考: {referCsvPath}");
            }
            catch (Exception ex)
            {
                LogUpdate($"CICD CSV对比失败: {ex.Message}");
                MessageBox.Show($"CICD CSV对比失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryResolveCicdReferenceCsvPath(string testCsvPath, out string templateName, out string referCsvPath, out string cicdRoot)
        {
            templateName = "";
            referCsvPath = "";
            cicdRoot = "";

            try
            {
                if (string.IsNullOrWhiteSpace(testCsvPath))
                {
                    return false;
                }

                var testFile = new FileInfo(testCsvPath);
                if (!testFile.Exists || testFile.Directory == null)
                {
                    return false;
                }

                var testDir = testFile.Directory; // ...\CICD检测\测试\yyyyMMdd
                string imageSetName = testDir.Name;

                // 优先按标准结构解析：...\CICD检测\测试\<ImageSetName>\*.csv
                var parent = testDir.Parent; // ...\测试
                if (parent != null && string.Equals(parent.Name, CicdTestFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    var cicdDir = parent.Parent; // ...\CICD检测
                    if (cicdDir != null && string.Equals(cicdDir.Name, CicdFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        cicdRoot = cicdDir.FullName;
                        var templateDir = cicdDir.Parent;
                        if (templateDir != null)
                        {
                            templateName = templateDir.Name ?? "";
                        }

                        referCsvPath = Path.Combine(cicdRoot, imageSetName, CicdReferCsvName);
                        return true;
                    }
                }

                // 兜底：向上查找CICD检测目录
                DirectoryInfo current = testDir;
                DirectoryInfo foundCicd = null;
                while (current != null)
                {
                    if (string.Equals(current.Name, CicdFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundCicd = current;
                        break;
                    }
                    current = current.Parent;
                }

                if (foundCicd == null)
                {
                    return false;
                }

                cicdRoot = foundCicd.FullName;
                var foundTemplateDir = foundCicd.Parent;
                if (foundTemplateDir != null)
                {
                    templateName = foundTemplateDir.Name ?? "";
                }

                // 如果testCsv就在...\CICD检测\测试\...\里，取其父目录名作为imageSetName
                referCsvPath = Path.Combine(cicdRoot, imageSetName, CicdReferCsvName);
                return true;
            }
            catch
            {
                templateName = "";
                referCsvPath = "";
                cicdRoot = "";
                return false;
            }
        }

        private List<CicdGroupSource> DiscoverCicdGroupSources(IEnumerable<string> sourceFolders)
        {
            var discovered = new List<CicdGroupSource>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in sourceFolders ?? Enumerable.Empty<string>())
            {
                foreach (var sourceRoot in DiscoverSourceRoots(folder))
                {
                    string baseName = SanitizeFolderName(Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                    if (string.IsNullOrWhiteSpace(baseName))
                    {
                        baseName = "Group";
                    }

                    string groupName = baseName;
                    int i = 1;
                    while (usedNames.Contains(groupName))
                    {
                        groupName = $"{baseName}_{i++}";
                    }

                    usedNames.Add(groupName);
                    discovered.Add(new CicdGroupSource
                    {
                        GroupName = groupName,
                        SourceRoot = sourceRoot
                    });
                }
            }

            return discovered;
        }

        private List<CicdGroupSource> DiscoverCicdGroupSourcesFromFiles(IEnumerable<string> selectedFiles)
        {
            var selectedByRoot = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in selectedFiles ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                {
                    continue;
                }

                string groupRoot = ResolveCicdGroupRootFromFile(file);
                if (string.IsNullOrWhiteSpace(groupRoot) || !Directory.Exists(groupRoot))
                {
                    continue;
                }

                string source1File = ResolveCicdSource1File(groupRoot, file);
                if (string.IsNullOrWhiteSpace(source1File) || !File.Exists(source1File))
                {
                    continue;
                }

                if (!selectedByRoot.TryGetValue(groupRoot, out HashSet<string> set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    selectedByRoot[groupRoot] = set;
                }

                set.Add(source1File);
            }

            var discovered = new List<CicdGroupSource>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in selectedByRoot.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                string sourceRoot = kvp.Key;
                string baseName = SanitizeFolderName(Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    baseName = "Group";
                }

                string groupName = baseName;
                int i = 1;
                while (usedNames.Contains(groupName))
                {
                    groupName = $"{baseName}_{i++}";
                }

                usedNames.Add(groupName);

                var selectedSource1Files = kvp.Value
                    .Where(File.Exists)
                    .OrderBy(f => ExtractImageNumber(Path.GetFileName(f)))
                    .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                discovered.Add(new CicdGroupSource
                {
                    GroupName = groupName,
                    SourceRoot = sourceRoot,
                    SelectedSource1Files = selectedSource1Files
                });
            }

            return discovered;
        }

        private string ResolveCicdGroupRootFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return null;
                }

                string dir = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(dir))
                {
                    return null;
                }

                string dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (IsCicdSourceFolderName(dirName))
                {
                    return Path.GetDirectoryName(dir);
                }

                if (Directory.Exists(Path.Combine(dir, "图像源1")))
                {
                    return dir;
                }

                string current = dir;
                for (int i = 0; i < 6; i++)
                {
                    string parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrWhiteSpace(parent))
                    {
                        break;
                    }

                    if (Directory.Exists(Path.Combine(parent, "图像源1")))
                    {
                        return parent;
                    }

                    current = parent;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private bool IsCicdSourceFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            return string.Equals(folderName, "图像源1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(folderName, "图像源2_1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(folderName, "图像源2_2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(folderName, "3D", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveCicdSource1File(string groupRoot, string selectedFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(groupRoot) || string.IsNullOrWhiteSpace(selectedFilePath))
                {
                    return null;
                }

                string source1Dir = Path.Combine(groupRoot, "图像源1");
                if (!Directory.Exists(source1Dir))
                {
                    return null;
                }

                string selectedFull = Path.GetFullPath(selectedFilePath);
                string source1FullDir = Path.GetFullPath(source1Dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
                if (selectedFull.StartsWith(source1FullDir, StringComparison.OrdinalIgnoreCase) && File.Exists(selectedFull))
                {
                    return selectedFull;
                }

                string suffix = GetFileSuffix(Path.GetFileName(selectedFull));
                if (string.IsNullOrWhiteSpace(suffix))
                {
                    return null;
                }

                return FindFirstFileInFolder(source1Dir, $"*{suffix}");
            }
            catch
            {
                return null;
            }
        }

        private List<string> DiscoverSourceRoots(string rootFolder)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();
            pending.Enqueue(rootFolder);

            while (pending.Count > 0)
            {
                string current = pending.Dequeue();
                if (string.IsNullOrWhiteSpace(current) || !Directory.Exists(current))
                {
                    continue;
                }

                try
                {
                    string name = Path.GetFileName(current);
                    if (string.Equals(name, "图像源1", StringComparison.OrdinalIgnoreCase))
                    {
                        string parent = Path.GetDirectoryName(current);
                        if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                        {
                            roots.Add(parent);
                        }
                    }

                    foreach (var subDir in Directory.GetDirectories(current))
                    {
                        pending.Enqueue(subDir);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return roots.ToList();
        }

        private List<ImageGroupSet> LoadCicdImageGroupsFromSelectedSource1Files(string groupRoot, string groupName, List<string> selectedSource1Files)
        {
            var imageGroups = new List<ImageGroupSet>();
            try
            {
                if (string.IsNullOrWhiteSpace(groupRoot) || selectedSource1Files == null || selectedSource1Files.Count == 0)
                {
                    return imageGroups;
                }

                string source2_1Dir = Path.Combine(groupRoot, "图像源2_1");
                string source2_2Dir = Path.Combine(groupRoot, "图像源2_2");
                string threeDDir = Path.Combine(groupRoot, "3D");

                var orderedSource1Files = selectedSource1Files
                    .Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => ExtractImageNumber(Path.GetFileName(f)))
                    .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (int i = 0; i < orderedSource1Files.Count; i++)
                {
                    string source1File = orderedSource1Files[i];
                    string suffix = GetFileSuffix(Path.GetFileName(source1File));

                    var imageGroup = new ImageGroupSet
                    {
                        BaseName = $"{groupName}_第{i + 1}组",
                        Source1Path = source1File
                    };

                    if (Directory.Exists(source2_1Dir))
                    {
                        imageGroup.Source2_1Path = FindFirstFileInFolder(source2_1Dir, $"*{suffix}");
                    }

                    if (Directory.Exists(source2_2Dir))
                    {
                        imageGroup.Source2_2Path = FindFirstFileInFolder(source2_2Dir, $"*{suffix}");
                    }

                    if (Directory.Exists(threeDDir))
                    {
                        imageGroup.HeightImagePath = Directory.GetFiles(threeDDir, $"height*{suffix}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        imageGroup.GrayImagePath = Directory.GetFiles(threeDDir, $"gray*{suffix}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    }

                    if (imageGroup.IsValid)
                    {
                        imageGroups.Add(imageGroup);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载CICD图片组失败: {ex.Message}");
            }

            return imageGroups;
        }

        private List<ImageGroupSet> LoadCicdImageGroupsFromGroupFolder(string groupRoot, string groupName)
        {
            var imageGroups = new List<ImageGroupSet>();
            try
            {
                string source1Dir = Path.Combine(groupRoot, "图像源1");
                string source2_1Dir = Path.Combine(groupRoot, "图像源2_1");
                string source2_2Dir = Path.Combine(groupRoot, "图像源2_2");
                string threeDDir = Path.Combine(groupRoot, "3D");

                if (!Directory.Exists(source1Dir))
                {
                    return imageGroups;
                }

                var source1Files = Directory.GetFiles(source1Dir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => ExtractImageNumber(Path.GetFileName(f)))
                    .ToList();

                for (int i = 0; i < source1Files.Count; i++)
                {
                    string source1File = source1Files[i];
                    string suffix = GetFileSuffix(Path.GetFileName(source1File));

                    var imageGroup = new ImageGroupSet
                    {
                        BaseName = $"{groupName}_第{i + 1}组",
                        Source1Path = source1File
                    };

                    if (Directory.Exists(source2_1Dir))
                    {
                        imageGroup.Source2_1Path = FindFirstFileInFolder(source2_1Dir, $"*{suffix}");
                    }

                    if (Directory.Exists(source2_2Dir))
                    {
                        imageGroup.Source2_2Path = FindFirstFileInFolder(source2_2Dir, $"*{suffix}");
                    }

                    if (Directory.Exists(threeDDir))
                    {
                        imageGroup.HeightImagePath = Directory.GetFiles(threeDDir, $"height*{suffix}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        imageGroup.GrayImagePath = Directory.GetFiles(threeDDir, $"gray*{suffix}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    }

                    if (imageGroup.IsValid)
                    {
                        imageGroups.Add(imageGroup);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载CICD图片组失败: {ex.Message}");
            }

            return imageGroups;
        }

        private string FindFirstFileInFolder(string folder, string patternWithoutExtension)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    return null;
                }

                foreach (var ext in new[] { ".bmp", ".png" })
                {
                    string pattern = patternWithoutExtension.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                        ? patternWithoutExtension
                        : $"{patternWithoutExtension}{ext}";

                    var file = Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        return file;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void CopyCicdGroupFolderAll(string sourceRoot, string targetGroupFolder)
        {
            Directory.CreateDirectory(targetGroupFolder);

            foreach (var folderName in new[] { "图像源1", "图像源2_1", "图像源2_2", "3D" })
            {
                string src = Path.Combine(sourceRoot, folderName);
                if (!Directory.Exists(src))
                {
                    continue;
                }

                string dst = Path.Combine(targetGroupFolder, folderName);
                Directory.CreateDirectory(dst);

                foreach (var file in Directory.GetFiles(src))
                {
                    string dstFile = Path.Combine(dst, Path.GetFileName(file));
                    File.Copy(file, dstFile, true);
                }
            }
        }

        private void CopyCicdGroupFolderSelected(string sourceRoot, string targetGroupFolder, List<string> selectedSource1Files)
        {
            Directory.CreateDirectory(targetGroupFolder);

            string dstSource1Dir = Path.Combine(targetGroupFolder, "图像源1");
            string dstSource2_1Dir = Path.Combine(targetGroupFolder, "图像源2_1");
            string dstSource2_2Dir = Path.Combine(targetGroupFolder, "图像源2_2");
            string dst3DDir = Path.Combine(targetGroupFolder, "3D");

            Directory.CreateDirectory(dstSource1Dir);
            Directory.CreateDirectory(dstSource2_1Dir);
            Directory.CreateDirectory(dstSource2_2Dir);
            Directory.CreateDirectory(dst3DDir);

            var groups = LoadCicdImageGroupsFromSelectedSource1Files(sourceRoot, Path.GetFileName(targetGroupFolder), selectedSource1Files);
            foreach (var group in groups)
            {
                CopyFileToDir(group.Source1Path, dstSource1Dir);
                CopyFileToDir(group.Source2_1Path, dstSource2_1Dir);
                CopyFileToDir(group.Source2_2Path, dstSource2_2Dir);
                CopyFileToDir(group.HeightImagePath, dst3DDir);
                CopyFileToDir(group.GrayImagePath, dst3DDir);
            }
        }

        private void CopyFileToDir(string sourceFile, string targetDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceFile) || string.IsNullOrWhiteSpace(targetDir) || !File.Exists(sourceFile))
                {
                    return;
                }

                Directory.CreateDirectory(targetDir);
                string dst = Path.Combine(targetDir, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, dst, true);
            }
            catch
            {
                // ignore
            }
        }

        private void WriteCicdCsv(string csvPath, List<TestModeDetectionResult> results, List<CicdGroupSource> groupSources = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath));

            var itemNames = results
                .SelectMany(r => r?.DetectionItems ?? new List<DetectionItem>())
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => i.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                var header = new List<string> { "组名", "序号", "时间戳", "结果", "缺陷类型" };
                header.AddRange(itemNames);
                writer.WriteLine(string.Join(",", header.Select(EscapeCsv)));

                foreach (var result in results)
                {
                    string groupName = ResolveCicdGroupName(result?.ImagePath, groupSources) ?? "";
                    var itemMap = (result?.DetectionItems ?? new List<DetectionItem>())
                        .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                        .GroupBy(i => i.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.LastOrDefault()?.Value ?? "", StringComparer.OrdinalIgnoreCase);

                    var row = new List<string>
                    {
                        groupName,
                        result?.ImageNumber ?? "",
                        result?.TestTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        result != null && result.IsOK ? "OK" : "NG",
                        result?.DefectType ?? ""
                    };

                    foreach (var itemName in itemNames)
                    {
                        row.Add(itemMap.TryGetValue(itemName, out string value) ? value ?? "" : "");
                    }

                    writer.WriteLine(string.Join(",", row.Select(EscapeCsv)));
                }
            }
        }

        private string ResolveCicdGroupName(string imagePath, List<CicdGroupSource> groupSources)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    return null;
                }

                if (groupSources != null && groupSources.Count > 0)
                {
                    string fullImagePath = Path.GetFullPath(imagePath);
                    CicdGroupSource matched = null;
                    int matchedLen = -1;

                    foreach (var group in groupSources)
                    {
                        if (string.IsNullOrWhiteSpace(group?.SourceRoot))
                        {
                            continue;
                        }

                        string root = Path.GetFullPath(group.SourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
                        if (fullImagePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        {
                            if (root.Length > matchedLen)
                            {
                                matchedLen = root.Length;
                                matched = group;
                            }
                        }
                    }

                    if (matched != null && !string.IsNullOrWhiteSpace(matched.GroupName))
                    {
                        return matched.GroupName;
                    }
                }

                return TryExtractGroupNameFromImagePath(imagePath);
            }
            catch
            {
                return TryExtractGroupNameFromImagePath(imagePath);
            }
        }

        private string CompareCicdCsvFiles(string referCsvPath, string testCsvPath, string templateName, out bool isPass)
        {
            isPass = false;
            try
            {
                var standard = CicdAcceptanceCriteriaConfigManager.GetActiveStandard(templateName);
                var refer = ParseCicdCsv(referCsvPath);
                var test = ParseCicdCsv(testCsvPath);

                var report = new StringBuilder();
                report.AppendLine("CICD对比报告");
                report.AppendLine($"基准: {referCsvPath}");
                report.AppendLine($"测试: {testCsvPath}");
                report.AppendLine($"模板: {templateName}");
                report.AppendLine($"标准: {standard?.Name}");
                report.AppendLine($"基准行数: {refer.Count}");
                report.AppendLine($"测试行数: {test.Count}");
                report.AppendLine();

                var referByKey = refer
                    .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
                var testByKey = test
                    .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

                var missingKeys = referByKey.Keys.Except(testByKey.Keys).OrderBy(k => k).ToList();
                var extraKeys = testByKey.Keys.Except(referByKey.Keys).OrderBy(k => k).ToList();

                int okNgMismatch = 0;
                int defectTypeMismatch = 0;
                var okNgMismatchDetails = new List<string>();
                var defectTypeMismatchDetails = new List<string>();

                foreach (var key in referByKey.Keys.Intersect(testByKey.Keys))
                {
                    var a = referByKey[key];
                    var b = testByKey[key];

                    if (a.IsOK != b.IsOK)
                    {
                        okNgMismatch++;
                        if (okNgMismatchDetails.Count < 50)
                        {
                            okNgMismatchDetails.Add($"{key},基准={(a.IsOK ? "OK" : "NG")},测试={(b.IsOK ? "OK" : "NG")}");
                        }
                        continue;
                    }

                    // 分类对比：仅在双方均为NG时对比缺陷类型，OK行不纳入分类对比
                    if (!a.IsOK && !b.IsOK &&
                        !string.Equals(a.DefectType ?? "", b.DefectType ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        defectTypeMismatch++;
                        if (defectTypeMismatchDetails.Count < 50)
                        {
                            defectTypeMismatchDetails.Add($"{key},基准={a.DefectType ?? ""},测试={b.DefectType ?? ""}");
                        }
                    }
                }

                report.AppendLine($"缺失行: {missingKeys.Count}");
                report.AppendLine($"新增行: {extraKeys.Count}");
                report.AppendLine($"OK/NG不一致: {okNgMismatch}");
                report.AppendLine($"分类不一致: {defectTypeMismatch}");
                report.AppendLine();

                if (missingKeys.Count > 0)
                {
                    report.AppendLine("缺失行明细（基准有，测试无，最多50条）：");
                    foreach (var key in missingKeys.Take(50))
                    {
                        report.AppendLine(key);
                    }
                    if (missingKeys.Count > 50)
                    {
                        report.AppendLine($"... 其余 {missingKeys.Count - 50} 条省略");
                    }
                    report.AppendLine();
                }

                if (extraKeys.Count > 0)
                {
                    report.AppendLine("新增行明细（测试有，基准无，最多50条）：");
                    foreach (var key in extraKeys.Take(50))
                    {
                        report.AppendLine(key);
                    }
                    if (extraKeys.Count > 50)
                    {
                        report.AppendLine($"... 其余 {extraKeys.Count - 50} 条省略");
                    }
                    report.AppendLine();
                }

                if (okNgMismatchDetails.Count > 0)
                {
                    report.AppendLine("OK/NG不一致明细（最多50条）：");
                    foreach (var line in okNgMismatchDetails)
                    {
                        report.AppendLine(line);
                    }
                    report.AppendLine();
                }

                if (defectTypeMismatchDetails.Count > 0)
                {
                    report.AppendLine("分类不一致明细（最多50条）：");
                    foreach (var line in defectTypeMismatchDetails)
                    {
                        report.AppendLine(line);
                    }
                    report.AppendLine();
                }

                double toleranceAbs = standard?.DefaultNumericToleranceAbs ?? 0.0;
                double toleranceRatio = standard?.DefaultNumericToleranceRatio ?? 0.0;
                var rangeDiffs = CompareNumericRanges(refer, test, standard);
                report.AppendLine($"数值极差不一致: {rangeDiffs.Count}");
                foreach (var line in rangeDiffs.Take(50))
                {
                    report.AppendLine(line);
                }

                report.AppendLine();
                report.AppendLine("判定规则：");
                report.AppendLine($"- 缺失行/新增行必须为0（当前：缺失{missingKeys.Count}，新增{extraKeys.Count}）");
                report.AppendLine($"- OK/NG不一致 <= {standard?.AllowedOkNgMismatchCount ?? 0}（当前：{okNgMismatch}）");
                report.AppendLine($"- 分类不一致 <= {standard?.AllowedDefectTypeMismatchCount ?? 0}（当前：{defectTypeMismatch}）");
                report.AppendLine($"- 极差不一致项数 <= {standard?.AllowedNumericRangeMismatchCount ?? 0}（当前：{rangeDiffs.Count}）");
                int overrideCount = standard?.ItemTolerances != null ? standard.ItemTolerances.Count : 0;
                report.AppendLine($"- 极差阈值 = max(默认绝对容差{toleranceAbs}, 基准极差*默认比例容差{toleranceRatio})（按检测项可单独配置，覆盖项: {overrideCount}）");
                report.AppendLine();

                isPass = missingKeys.Count == 0
                         && extraKeys.Count == 0
                         && okNgMismatch <= (standard?.AllowedOkNgMismatchCount ?? 0)
                         && defectTypeMismatch <= (standard?.AllowedDefectTypeMismatchCount ?? 0)
                         && rangeDiffs.Count <= (standard?.AllowedNumericRangeMismatchCount ?? 0);

                report.AppendLine($"判定结果: {(isPass ? "通过" : "未通过")}");

                return report.ToString();
            }
            catch (Exception ex)
            {
                return $"CICD对比失败: {ex.Message}";
            }
        }

        private List<string> CompareNumericRanges(List<CicdCsvRow> refer, List<CicdCsvRow> test, CicdAcceptanceCriteriaStandard standard)
        {
            var diffs = new List<string>();

            var tolMap = new Dictionary<string, CicdItemToleranceConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var tol in standard?.ItemTolerances ?? new List<CicdItemToleranceConfig>())
            {
                if (tol == null || string.IsNullOrWhiteSpace(tol.ItemName))
                {
                    continue;
                }

                tolMap[tol.ItemName.Trim()] = tol;
            }

            double defaultAbs = standard?.DefaultNumericToleranceAbs ?? 0.0;
            double defaultRatio = standard?.DefaultNumericToleranceRatio ?? 0.0;

            var referByGroup = refer.GroupBy(r => r.GroupName ?? "", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var testByGroup = test.GroupBy(r => r.GroupName ?? "", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var groupName in referByGroup.Keys.Intersect(testByGroup.Keys))
            {
                var aRows = referByGroup[groupName];
                var bRows = testByGroup[groupName];

                var itemNames = aRows.SelectMany(r => r.Values.Keys)
                    .Union(bRows.SelectMany(r => r.Values.Keys), StringComparer.OrdinalIgnoreCase)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var itemName in itemNames)
                {
                    if (string.Equals(itemName, "时间戳", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var aValues = aRows.Select(r => r.Values.TryGetValue(itemName, out double? v) ? v : null)
                        .Where(v => v.HasValue)
                        .Select(v => v.Value)
                        .ToList();
                    var bValues = bRows.Select(r => r.Values.TryGetValue(itemName, out double? v) ? v : null)
                        .Where(v => v.HasValue)
                        .Select(v => v.Value)
                        .ToList();

                    if (aValues.Count == 0 || bValues.Count == 0)
                    {
                        continue;
                    }

                    double aRange = aValues.Max() - aValues.Min();
                    double bRange = bValues.Max() - bValues.Min();
                    double diff = Math.Abs(bRange - aRange);

                    double absTol = defaultAbs;
                    double ratioTol = defaultRatio;
                    if (tolMap.TryGetValue(itemName, out var tol) && tol != null)
                    {
                        absTol = tol.ToleranceAbs;
                        ratioTol = tol.ToleranceRatio < 0 ? 0 : tol.ToleranceRatio;
                    }

                    double threshold = Math.Max(absTol, Math.Abs(aRange) * ratioTol);

                    if (diff > threshold)
                    {
                        diffs.Add($"{groupName},{itemName},基准极差={aRange:F4},测试极差={bRange:F4},差值={diff:F4},阈值={threshold:F4}");
                    }
                }
            }

            return diffs;
        }

        private sealed class CicdCsvRow
        {
            public string GroupName { get; set; }
            public string ImageNumber { get; set; }
            public bool IsOK { get; set; }
            public string DefectType { get; set; }
            public Dictionary<string, double?> Values { get; set; } = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

            public string Key => $"{GroupName ?? ""}#{ImageNumber ?? ""}";
        }

        private List<CicdCsvRow> ParseCicdCsv(string csvPath)
        {
            var rows = new List<CicdCsvRow>();
            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return rows;
            }

            var header = SplitCsvLine(lines[0]).Select(h => h?.Trim() ?? "").ToList();
            int idxGroup = header.FindIndex(h => string.Equals(h, "组名", StringComparison.OrdinalIgnoreCase));
            int idxNumber = header.FindIndex(h => string.Equals(h, "序号", StringComparison.OrdinalIgnoreCase));
            int idxResult = header.FindIndex(h => string.Equals(h, "结果", StringComparison.OrdinalIgnoreCase));
            int idxType = header.FindIndex(h => string.Equals(h, "缺陷类型", StringComparison.OrdinalIgnoreCase));

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                var parts = SplitCsvLine(lines[i]);
                var row = new CicdCsvRow
                {
                    GroupName = idxGroup >= 0 && idxGroup < parts.Count ? parts[idxGroup] : "",
                    ImageNumber = idxNumber >= 0 && idxNumber < parts.Count ? parts[idxNumber] : "",
                    IsOK = idxResult >= 0 && idxResult < parts.Count && string.Equals(parts[idxResult], "OK", StringComparison.OrdinalIgnoreCase),
                    DefectType = idxType >= 0 && idxType < parts.Count ? parts[idxType] : ""
                };

                for (int c = 0; c < header.Count && c < parts.Count; c++)
                {
                    string col = header[c];
                    if (string.IsNullOrWhiteSpace(col))
                    {
                        continue;
                    }

                    if (c == idxGroup || c == idxNumber || c == idxResult || c == idxType)
                    {
                        continue;
                    }

                    if (TryParseDouble(parts[c], out double value))
                    {
                        row.Values[col] = value;
                    }
                }

                rows.Add(row);
            }

            return rows;
        }

        private bool TryParseDouble(string raw, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            raw = raw.Trim();
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private string TryExtractGroupNameFromImagePath(string imagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    return null;
                }

                string currentDir = Path.GetDirectoryName(imagePath);
                while (!string.IsNullOrWhiteSpace(currentDir))
                {
                    string folderName = Path.GetFileName(currentDir);
                    if (string.Equals(folderName, "图像源1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(folderName, "图像源2_1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(folderName, "图像源2_2", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(folderName, "3D", StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetFileName(Path.GetDirectoryName(currentDir));
                    }

                    currentDir = Path.GetDirectoryName(currentDir);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string PromptForTextSimple(string title, string message, string defaultValue)
        {
            var window = new Window
            {
                Title = title,
                Width = 460,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            var root = new System.Windows.Controls.Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var text = new System.Windows.Controls.TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8) };
            System.Windows.Controls.Grid.SetRow(text, 0);
            root.Children.Add(text);

            var textBox = new System.Windows.Controls.TextBox { Text = defaultValue ?? "", Margin = new Thickness(0, 0, 0, 12) };
            System.Windows.Controls.Grid.SetRow(textBox, 1);
            root.Children.Add(textBox);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            string result = null;
            var ok = new System.Windows.Controls.Button
            {
                Content = "确定",
                Width = 90,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            ok.Click += (s, e) =>
            {
                result = textBox.Text;
                window.DialogResult = true;
                window.Close();
            };

            var cancel = new System.Windows.Controls.Button
            {
                Content = "取消",
                Width = 90,
                IsCancel = true
            };
            cancel.Click += (s, e) =>
            {
                result = null;
                window.DialogResult = false;
                window.Close();
            };

            buttonPanel.Children.Add(ok);
            buttonPanel.Children.Add(cancel);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);
            root.Children.Add(buttonPanel);

            window.Content = root;
            window.ShowDialog();
            return result;
        }

        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }

            foreach (var ch in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(ch, '_');
            }

            return name.Trim();
        }

        private string EscapeCsv(string value)
        {
            if (value == null)
            {
                value = "";
            }
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        private List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null)
            {
                return result;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(ch);
            }

            result.Add(sb.ToString());
            return result;
        }
    }
}
