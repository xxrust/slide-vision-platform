using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Slide.Platform.Abstractions;

namespace Slide.Platform.Runtime
{
    public static class PluginLoader
    {
        public static PluginLoadResult LoadFromDirectory(string directory)
        {
            var result = new PluginLoadResult();
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                result.Errors.Add($"插件目录不存在: {directory}");
                return result;
            }

            foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                TryLoadFromAssemblyPath(dll, result);
            }

            return result;
        }

        private static void TryLoadFromAssemblyPath(string assemblyPath, PluginLoadResult result)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                foreach (var plugin in CreatePluginsFromAssembly(assembly, result))
                {
                    result.Plugins.Add(plugin);
                }
            }
            catch (BadImageFormatException)
            {
                // Native DLL or non-.NET assembly; ignore.
            }
            catch (Exception ex)
            {
                result.Errors.Add($"加载程序集失败: {Path.GetFileName(assemblyPath)} - {ex.Message}");
            }
        }

        private static IEnumerable<IAlgorithmPlugin> CreatePluginsFromAssembly(Assembly assembly, PluginLoadResult result)
        {
            var pluginType = typeof(IAlgorithmPlugin);
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
                result.Errors.Add($"扫描程序集类型失败: {assembly.GetName().Name} - {ex.Message}");
            }

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || !pluginType.IsAssignableFrom(type))
                {
                    continue;
                }

                IAlgorithmPlugin instance = null;
                try
                {
                    instance = (IAlgorithmPlugin)Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"实例化插件失败: {type.FullName} - {ex.Message}");
                }

                if (instance != null)
                {
                    yield return instance;
                }
            }
        }
    }
}
