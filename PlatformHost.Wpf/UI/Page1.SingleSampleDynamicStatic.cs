using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class Page1
    {
        private const string SingleSampleDynamicStaticImageSetFolderName = "单片动态静态图片集";

        private async Task StartSingleSampleDynamicStaticCollectionMode(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    LogUpdate("单片动态/静态测试集制作失败：文件夹无效");
                    MessageBox.Show("请选择一个有效的文件夹", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                LoadingDialog loadingDialog = null;
                string errorMessage = null;
                string errorTitle = null;
                string successMessage = null;

                try
                {
                    loadingDialog = new LoadingDialog("正在制作单片动态/静态测试集，请稍候...");
                    loadingDialog.Owner = Application.Current.MainWindow;
                    loadingDialog.Show();
                    await Task.Delay(100);

                    var searchResult = await Task.Run(() => SearchAndGroupValidatorMachineImages(folderPath, 1));
                    if (searchResult.HasError)
                    {
                        errorMessage = searchResult.ErrorMessage;
                        errorTitle = searchResult.ErrorTitle ?? "错误";
                        return;
                    }

                    var imageGroups = searchResult.ImageGroups;
                    int loopCycle = searchResult.LoopCycle;
                    if (imageGroups == null || imageGroups.Count == 0 || loopCycle <= 0)
                    {
                        errorMessage = "未找到任何可用图片，请检查文件夹结构是否包含“图像源1”等子文件夹";
                        errorTitle = "未找到图片";
                        return;
                    }

                    string savedFolder = await SaveSingleSampleDynamicStaticImageSet(imageGroups, folderPath);
                    if (string.IsNullOrEmpty(savedFolder))
                    {
                        errorMessage = "测试集保存失败";
                        errorTitle = "错误";
                        return;
                    }

                    LogUpdate($"单片动态/静态测试集制作完成：{Path.GetFileName(savedFolder)}，已保存到模板目录");
                    successMessage = $"单片动态/静态测试集制作完成：{Path.GetFileName(savedFolder)}\n\n" +
                        $"巡回次数: {loopCycle}\n\n" +
                        $"已保存至:\n{savedFolder}\n\n" +
                        $"请在「单片动态/静态测试」中选择该图片集进行检测。";
                }
                finally
                {
                    if (loadingDialog != null)
                    {
                        await Task.Delay(200);
                        loadingDialog.Close();
                    }

                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        MessageBoxImage icon = errorTitle == "图片缺失警告" ? MessageBoxImage.Warning : MessageBoxImage.Information;
                        MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, icon);
                    }
                    else if (!string.IsNullOrEmpty(successMessage))
                    {
                        MessageBox.Show(successMessage, "制作完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"单片动态/静态测试集制作失败: {ex.Message}");
                MessageBox.Show($"单片动态/静态测试集制作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartSingleSampleDynamicStaticDetectionMode(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    LogUpdate("单片动态/静态测试失败：文件夹无效");
                    MessageBox.Show("请选择一个有效的图片集文件夹", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string source1Dir = Path.Combine(folderPath, "图号1", "图像源1");
                if (!Directory.Exists(source1Dir))
                {
                    LogUpdate("单片动态/静态测试失败：未找到图号1/图像源1目录");
                    MessageBox.Show("未找到图号1/图像源1目录，请确认该文件夹为单片动态/静态图片集", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int loopCycle = Directory.GetFiles(source1Dir, "*.bmp").Length;
                if (loopCycle <= 0)
                {
                    LogUpdate("单片动态/静态测试失败：图像源1内无bmp图片");
                    MessageBox.Show("图像源1内未找到bmp图片", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await StartValidatorMachineDetectionMode(folderPath, 1, loopCycle);
            }
            catch (Exception ex)
            {
                LogUpdate($"单片动态/静态测试失败: {ex.Message}");
                MessageBox.Show($"单片动态/静态测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> SaveSingleSampleDynamicStaticImageSet(List<ValidatorMachineImageGroup> imageGroups, string sourcePath)
        {
            try
            {
                string setName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar));
                string templateDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "templates",
                    CurrentTemplateName,
                    SingleSampleDynamicStaticImageSetFolderName,
                    setName
                );

                if (Directory.Exists(templateDir))
                {
                    Directory.Delete(templateDir, true);
                    await Task.Delay(100);
                }

                foreach (var sampleGroup in imageGroups)
                {
                    string sampleDir = Path.Combine(templateDir, $"图号{sampleGroup.SampleNumber}");

                    var sourceStructureMap = new Dictionary<string, List<string>>();
                    foreach (var imagePath in sampleGroup.ImagePaths)
                    {
                        string sourceStructureRoot = FindSourceStructureRoot(imagePath);
                        if (string.IsNullOrEmpty(sourceStructureRoot))
                        {
                            continue;
                        }

                        if (!sourceStructureMap.ContainsKey(sourceStructureRoot))
                        {
                            sourceStructureMap[sourceStructureRoot] = new List<string>();
                        }

                        sourceStructureMap[sourceStructureRoot].Add(imagePath);
                    }

                    foreach (var kvp in sourceStructureMap)
                    {
                        await CopyImageSourceStructure(kvp.Key, sampleDir, kvp.Value);
                    }
                }

                return templateDir;
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存单片动态/静态图片集失败: {ex.Message}");
                return null;
            }
        }
    }
}
