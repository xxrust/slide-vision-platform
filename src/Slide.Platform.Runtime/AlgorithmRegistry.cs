using System;
using System.Collections.Generic;
using Slide.Platform.Abstractions;

namespace Slide.Platform.Runtime
{
    public sealed class AlgorithmRegistry
    {
        private readonly Dictionary<string, IAlgorithmPlugin> _plugins = new Dictionary<string, IAlgorithmPlugin>(StringComparer.OrdinalIgnoreCase);

        public void Register(IAlgorithmPlugin plugin)
        {
            if (plugin == null || plugin.Descriptor == null)
            {
                return;
            }

            _plugins[plugin.Descriptor.Id] = plugin;
        }

        public void RegisterRange(IEnumerable<IAlgorithmPlugin> plugins)
        {
            if (plugins == null)
            {
                return;
            }

            foreach (var plugin in plugins)
            {
                Register(plugin);
            }
        }

        public IAlgorithmPlugin Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            _plugins.TryGetValue(id, out var plugin);
            return plugin;
        }

        public IReadOnlyCollection<IAlgorithmPlugin> List()
        {
            return _plugins.Values;
        }
    }
}
