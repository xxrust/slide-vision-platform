using System.Collections.Generic;
using System.IO;
using System.Linq;
using WpfApp2.Models;

namespace WpfApp2.UI
{
    internal static class ImageSourceNaming
    {
        public static int GetActiveSourceCount()
        {
            var sources = GetActiveImageSources();
            var count = sources?.Count ?? 0;
            return count > 0 ? count : 1;
        }

        public static IReadOnlyList<string> GetFolderCandidates(int index)
        {
            var candidates = new List<string>();
            var sources = GetActiveImageSources();
            if (index >= 0 && sources != null && index < sources.Count)
            {
                var displayName = sources[index]?.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    candidates.Add(displayName);
                }

                var id = sources[index]?.Id;
                if (!string.IsNullOrWhiteSpace(id) && !candidates.Contains(id))
                {
                    candidates.Add(id);
                }
            }

            var fallback = $"图像源{index + 1}";
            if (!candidates.Contains(fallback))
            {
                candidates.Add(fallback);
            }

            var fallbackId = $"Image{index + 1}";
            if (!candidates.Contains(fallbackId))
            {
                candidates.Add(fallbackId);
            }

            return candidates;
        }

        public static string GetActiveProfileId()
        {
            try
            {
                var templateFilePath = TemplateConfigPage.Instance?.CurrentTemplateFilePath;
                if (!string.IsNullOrWhiteSpace(templateFilePath) && File.Exists(templateFilePath))
                {
                    var template = TemplateParameters.LoadFromFile(templateFilePath);
                    if (!string.IsNullOrWhiteSpace(template?.ProfileId))
                    {
                        return template.ProfileId;
                    }
                }
            }
            catch
            {
                // Ignore and fall back to default profile id.
            }

            return TemplateHierarchyConfig.Instance.DefaultProfileId;
        }

        public static IReadOnlyList<ImageSourceDefinition> GetActiveImageSources()
        {
            var profileId = GetActiveProfileId();
            var profile = TemplateHierarchyConfig.Instance.ResolveProfile(profileId);
            if (profile?.ImageSources != null && profile.ImageSources.Count > 0)
            {
                return profile.ImageSources;
            }

            var fallbackProfile = TemplateHierarchyConfig.Instance.ResolveProfile(TemplateHierarchyConfig.Instance.DefaultProfileId);
            if (fallbackProfile?.ImageSources != null && fallbackProfile.ImageSources.Count > 0)
            {
                return fallbackProfile.ImageSources;
            }

            return new List<ImageSourceDefinition>
            {
                new ImageSourceDefinition { Id = "Image1", DisplayName = "图像1" }
            };
        }

        public static string GetDisplayName(int index)
        {
            var sources = GetActiveImageSources();
            if (index < 0 || index >= sources.Count)
            {
                return $"图像{index + 1}";
            }

            var name = sources[index]?.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? $"图像{index + 1}" : name;
        }

        public static IReadOnlyList<string> GetDisplayNames()
        {
            var sources = GetActiveImageSources();
            var names = new List<string>(sources.Count);
            for (int i = 0; i < sources.Count; i++)
            {
                var name = sources[i]?.DisplayName;
                names.Add(string.IsNullOrWhiteSpace(name) ? $"图像{i + 1}" : name);
            }

            return names;
        }

        public static string BuildDisplayNameList()
        {
            var names = GetDisplayNames();
            if (names.Count == 0)
            {
                return "图像";
            }

            if (names.Count == 1)
            {
                return names[0];
            }

            return string.Join("、", names);
        }
    }
}
