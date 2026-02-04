using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Slide.Algorithm.Contracts;
using ContractsAlgorithmInput = Slide.Algorithm.Contracts.AlgorithmInput;
using Microsoft.Win32;
using WpfApp2.Models;
using WpfApp2.Algorithms;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class Page1
    {
        private void ApplyRenderDefaults()
        {
            try
            {
                if (render1 != null)
                {
                    render1.Visibility = Visibility.Collapsed;
                }

                if (coating != null)
                {
                    coating.Visibility = Visibility.Collapsed;
                }

                if (RenderPreviewMain != null)
                {
                    RenderPreviewMain.Visibility = Visibility.Visible;
                }

                if (RenderPreviewStep != null)
                {
                    RenderPreviewStep.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"ApplyRenderDefaults failed: {ex.Message}");
            }
        }

        public async Task ExecuteAlgorithmPipelineForImageGroup(ImageGroupSet imageGroup, bool isTemplateConfig = false)
        {
            if (imageGroup == null)
            {
                LogUpdate("未选择有效的图片组");
                return;
            }

            if (!imageGroup.HasPrimary2DImage)
            {
                LogUpdate("图片组缺少主图，无法执行检测");
                return;
            }

            bool enable3D = Is3DDetectionEnabled() && imageGroup.Has3DImages;
            _detectionManager?.StartDetectionCycle(enable3D);

            var template = TryLoadCurrentTemplateParameters();
            string engineId = TemplateConfigPage.Instance?.CurrentAlgorithmEngineId;
            if (string.IsNullOrWhiteSpace(engineId))
            {
                engineId = template?.AlgorithmEngineId ?? AlgorithmEngineSettingsManager.PreferredEngineId;
            }

            var engine = AlgorithmEngineRegistry.ResolveEngine(engineId);
            if (engine == null)
            {
                LogUpdate("算法引擎未初始化，无法执行检测");
                return;
            }

            var input = BuildAlgorithmInput(imageGroup, template);

            var tasks = new List<Task>
            {
                ExecuteAlgorithmEngineDetectionAsync(engine, input)
            };

            if (enable3D)
            {
                tasks.Add(Execute3DDetection(imageGroup.HeightImagePath, imageGroup.GrayImagePath));
            }

            await Task.WhenAll(tasks);
        }

        private ContractsAlgorithmInput BuildAlgorithmInput(ImageGroupSet imageGroup, TemplateParameters template)
        {
            var input = new ContractsAlgorithmInput
            {
                TemplateName = template?.TemplateName ?? CurrentTemplateName ?? string.Empty,
                LotNumber = CurrentLotValue ?? string.Empty,
                ImageNumber = imageGroup?.BaseName ?? string.Empty
            };

            string profileId = template?.ProfileId;
            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = ImageSourceNaming.GetActiveProfileId();
            }

            var profile = TemplateHierarchyConfig.Instance.ResolveProfile(profileId);
            input.Parameters["TemplateProfileId"] = profileId ?? string.Empty;
            input.Parameters["TemplateProfileName"] = profile?.DisplayName ?? profileId ?? string.Empty;
            input.Parameters["SampleType"] = template?.SampleType.ToString() ?? string.Empty;
            input.Parameters["CoatingType"] = template?.CoatingType.ToString() ?? string.Empty;

            PopulateAlgorithmInputParameters(input, template);

            var sources = ImageSourceNaming.GetActiveImageSources();
            var displayNames = ImageSourceNaming.GetDisplayNames();
            input.Parameters["ImageSourceCount"] = sources.Count.ToString();

            for (int i = 0; i < displayNames.Count; i++)
            {
                input.Parameters[$"ImageSourceName{i + 1}"] = displayNames[i] ?? string.Empty;
            }

            if (imageGroup != null)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    string path = imageGroup.GetPath(i);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    string id = sources[i]?.Id;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        id = $"Image{i + 1}";
                    }

                    if (!input.ImagePaths.ContainsKey(id))
                    {
                        input.ImagePaths[id] = path;
                    }

                    if (i == 0 && !input.ImagePaths.ContainsKey("Image1"))
                    {
                        input.ImagePaths["Image1"] = path;
                    }

                    if (i == 1 && !input.ImagePaths.ContainsKey("Image2"))
                    {
                        input.ImagePaths["Image2"] = path;
                    }
                }
            }

            return input;
        }

        private async Task ExecuteCurrentImageGroup()
        {
            try
            {
                var currentGroup = _imageTestManager?.CurrentGroup;
                if (currentGroup == null)
                {
                    LogUpdate("当前没有可检测的图片组");
                    return;
                }

                if (!currentGroup.IsValid && !currentGroup.HasPrimary2DImage)
                {
                    LogUpdate("当前图片组无有效图像，无法检测");
                    return;
                }

                await ExecuteAlgorithmPipelineForImageGroup(currentGroup, isTemplateConfig: false);
            }
            catch (Exception ex)
            {
                LogUpdate($"执行图片检测失败: {ex.Message}");
            }
        }

        private async Task<List<ImageGroupSet>> SelectImageFilesAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
            {
                return new List<ImageGroupSet>();
            }

            var selected = dialog.FileNames ?? Array.Empty<string>();
            if (selected.Length == 0)
            {
                return new List<ImageGroupSet>();
            }

            var imageGroups = new List<ImageGroupSet>();
            var seenSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int requiredCount = ImageSourceNaming.GetActiveImageSources().Count;

            foreach (var file in selected)
            {
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(file);
                string suffix = ExtractSuffixFromFilename(fileName);

                if (requiredCount <= 1 || string.IsNullOrWhiteSpace(suffix))
                {
                    var group = new ImageGroupSet
                    {
                        BaseName = fileName
                    };
                    group.SetSource(0, file);
                    imageGroups.Add(group);
                    continue;
                }

                if (!seenSuffixes.Add(suffix))
                {
                    continue;
                }

                var sourceDir = Path.GetDirectoryName(file);
                var parentDir = Directory.GetParent(sourceDir ?? string.Empty)?.FullName;
                if (string.IsNullOrWhiteSpace(parentDir))
                {
                    var group = new ImageGroupSet
                    {
                        BaseName = fileName
                    };
                    group.SetSource(0, file);
                    imageGroups.Add(group);
                    continue;
                }

                var matchedGroup = CreateImageGroupBySuffix(parentDir, suffix);
                if (matchedGroup != null)
                {
                    imageGroups.Add(matchedGroup);
                }
            }

            await Task.CompletedTask;
            return imageGroups;
        }

        private static string ExtractSuffixFromFilename(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var match = Regex.Match(fileName, @"(_\d+)$");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            match = Regex.Match(fileName, @"(\d+)$");
            return match.Success ? match.Groups[1].Value : null;
        }

        private ImageGroupSet CreateImageGroupBySuffix(string parentDir, string suffix)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(parentDir) || string.IsNullOrWhiteSpace(suffix))
                {
                    return null;
                }

                var sources = ImageSourceNaming.GetActiveImageSources();
                int requiredCount = Math.Max(1, sources.Count);

                string baseName = null;
                var imageGroup = new ImageGroupSet();

                for (int i = 0; i < requiredCount; i++)
                {
                    foreach (var folder in BuildSourceFolderCandidates(sources, i))
                    {
                        var sourceDir = Path.Combine(parentDir, folder);
                        if (!Directory.Exists(sourceDir))
                        {
                            continue;
                        }

                        var sourceFiles = Directory.GetFiles(sourceDir, $"*{suffix}.bmp")
                            .Concat(Directory.GetFiles(sourceDir, $"*{suffix}.png"))
                            .ToArray();

                        if (sourceFiles.Length == 0)
                        {
                            continue;
                        }

                        string path = sourceFiles[0];
                        imageGroup.SetSource(i, path, sources[i]?.Id, sources[i]?.DisplayName);

                        if (string.IsNullOrWhiteSpace(baseName))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(path);
                            var match = Regex.Match(fileName, @"^(.+?)(?:_?\d+)$");
                            if (match.Success)
                            {
                                baseName = match.Groups[1].Value;
                            }
                        }

                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(imageGroup.Source1Path))
                {
                    return null;
                }

                imageGroup.BaseName = string.IsNullOrWhiteSpace(baseName)
                    ? $"{Path.GetFileName(parentDir)}{suffix}"
                    : $"{baseName}{suffix}";

                if (Is3DDetectionEnabled())
                {
                    FindAndSet3DImagesForGroup(parentDir, suffix, imageGroup, enableLogging: false);
                }

                return imageGroup;
            }
            catch (Exception ex)
            {
                LogManager.Error($"CreateImageGroupBySuffix failed: {ex.Message}");
                return null;
            }
        }

        private static List<string> BuildSourceFolderCandidates(IReadOnlyList<ImageSourceDefinition> sources, int index)
        {
            var candidates = new List<string>();

            if (sources != null && index >= 0 && index < sources.Count)
            {
                var source = sources[index];
                if (!string.IsNullOrWhiteSpace(source?.DisplayName))
                {
                    candidates.Add(source.DisplayName);
                }

                if (!string.IsNullOrWhiteSpace(source?.Id) && !candidates.Contains(source.Id))
                {
                    candidates.Add(source.Id);
                }
            }

            if (candidates.Count == 0)
            {
                candidates.Add($"Image{index + 1}");
            }

            return candidates;
        }
    }
}
