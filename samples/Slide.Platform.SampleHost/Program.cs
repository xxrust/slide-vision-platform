using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Slide.Platform.Abstractions;
using Slide.Platform.Runtime;

namespace Slide.Platform.SampleHost
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var pluginDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "plugins");
            var inputPath = args.Length > 1 ? args[1] : string.Empty;
            var pluginId = args.Length > 2 ? args[2] : "sample.basic";
            var modelPath = args.Length > 3 ? args[3] : string.Empty;

            if (args.Length == 3 && File.Exists(args[2]) && !File.Exists(args[1]))
            {
                // PowerShell drops empty arguments; treat (pluginId, modelPath) as args[1..2].
                inputPath = string.Empty;
                pluginId = args[1];
                modelPath = args[2];
            }

            Console.WriteLine($"PluginDir: {pluginDir}");
            Console.WriteLine($"InputPath: {inputPath}");
            Console.WriteLine($"PluginId : {pluginId}");
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                Console.WriteLine($"ModelPath: {modelPath}");
            }

            var loadResult = PluginLoader.LoadFromDirectory(pluginDir);
            foreach (var error in loadResult.Errors)
            {
                Console.WriteLine($"[LoadError] {error}");
            }

            if (loadResult.Plugins.Count == 0)
            {
                Console.WriteLine("未发现任何算法插件。");
                return 1;
            }

            var registry = new AlgorithmRegistry();
            registry.RegisterRange(loadResult.Plugins);

            var plugin = registry.Get(pluginId) ?? registry.List().FirstOrDefault();
            if (plugin == null)
            {
                Console.WriteLine("未找到可用插件。");
                return 2;
            }

            using (var session = plugin.CreateSession())
            {
                var input = new AlgorithmInput { ImagePath = inputPath };
                if (!string.IsNullOrWhiteSpace(modelPath))
                {
                    input.Parameters["ModelPath"] = modelPath;
                }
                var result = session.Run(input);
                PrintResult(plugin, result);
                return result.Success ? 0 : 3;
            }
        }

        private static void PrintResult(IAlgorithmPlugin plugin, AlgorithmResult result)
        {
            Console.WriteLine();
            Console.WriteLine($"Plugin: {plugin.Descriptor.Id} - {plugin.Descriptor.Name}");
            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"Message: {result.Message}");

            if (result.Metrics.Count == 0)
            {
                Console.WriteLine("No metrics.");
                return;
            }

            Console.WriteLine("Metrics:");
            foreach (var item in result.Metrics.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  {item.Key}: {item.Value.ToString("F4", CultureInfo.InvariantCulture)}");
            }
        }
    }
}
