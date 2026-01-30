using System;
using System.IO;
using Newtonsoft.Json;

namespace WpfApp2.Rendering
{
    public sealed class RendererSettings
    {
        public string RendererId { get; set; } = ImageRendererIds.File;
    }

    public static class RendererSettingsManager
    {
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "Renderer.json");
        private static RendererSettings _cached;

        public static string RendererId
        {
            get
            {
                EnsureLoaded();
                return _cached?.RendererId ?? ImageRendererIds.File;
            }
        }

        public static void UpdateRenderer(string rendererId)
        {
            EnsureLoaded();
            _cached.RendererId = NormalizeRendererId(rendererId);
            SaveInternal(_cached);
        }

        private static void EnsureLoaded()
        {
            if (_cached != null)
            {
                return;
            }

            _cached = LoadInternal();
        }

        private static RendererSettings LoadInternal()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var settings = JsonConvert.DeserializeObject<RendererSettings>(json) ?? new RendererSettings();
                    settings.RendererId = NormalizeRendererId(settings.RendererId);
                    return settings;
                }
            }
            catch (Exception)
            {
                // 忽略读取异常
            }

            return new RendererSettings();
        }

        private static void SaveInternal(RendererSettings settings)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception)
            {
                // 保存失败静默处理
            }
        }

        private static string NormalizeRendererId(string rendererId)
        {
            if (string.IsNullOrWhiteSpace(rendererId))
            {
                return ImageRendererIds.File;
            }

            return rendererId;
        }
    }
}
