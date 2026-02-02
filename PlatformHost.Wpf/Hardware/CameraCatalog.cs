using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace WpfApp2.Hardware
{
    public sealed class CameraDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class CameraCatalog
    {
        public List<CameraDefinition> Cameras { get; set; } = new List<CameraDefinition>();
    }

    public static class CameraCatalogManager
    {
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "CameraCatalog.json");
        private static List<CameraDefinition> _cameras;

        public static IReadOnlyList<CameraDefinition> GetCameras()
        {
            EnsureLoaded();
            return _cameras.Select(camera => new CameraDefinition
            {
                Id = camera.Id,
                Name = camera.Name
            }).ToList();
        }

        private static void EnsureLoaded()
        {
            if (_cameras != null)
            {
                return;
            }

            _cameras = LoadInternal();
            if (_cameras.Count == 0)
            {
                _cameras = GetDefaultCatalog();
            }
        }

        private static List<CameraDefinition> LoadInternal()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return new List<CameraDefinition>();
                }

                var json = File.ReadAllText(ConfigFile);
                var catalog = JsonConvert.DeserializeObject<CameraCatalog>(json) ?? new CameraCatalog();
                return NormalizeCatalog(catalog.Cameras);
            }
            catch
            {
                // Ignore read errors and fall back to defaults.
                return new List<CameraDefinition>();
            }
        }

        private static List<CameraDefinition> NormalizeCatalog(List<CameraDefinition> cameras)
        {
            var result = new List<CameraDefinition>();
            if (cameras == null)
            {
                return result;
            }

            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int autoIndex = 1;

            foreach (var camera in cameras)
            {
                if (camera == null)
                {
                    continue;
                }

                var id = (camera.Id ?? string.Empty).Trim();
                var name = (camera.Name ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(id))
                {
                    do
                    {
                        id = $"Camera{autoIndex++}";
                    }
                    while (usedIds.Contains(id));
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = id;
                }

                if (usedIds.Contains(id))
                {
                    continue;
                }

                usedIds.Add(id);
                result.Add(new CameraDefinition { Id = id, Name = name });
            }

            return result;
        }

        private static List<CameraDefinition> GetDefaultCatalog()
        {
            return new List<CameraDefinition>
            {
                new CameraDefinition { Id = "Flying", Name = "飞拍相机" },
                new CameraDefinition { Id = "Fixed", Name = "定拍相机" }
            };
        }
    }
}
