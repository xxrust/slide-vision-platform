using System;
using System.Collections.Generic;
using Slide.Platform.Abstractions;

namespace Slide.Platform.Runtime
{
    /// <summary>
    /// Instance-level engine registry that supports dynamic registration of
    /// both IAlgorithmEngine instances and legacy IAlgorithmPlugin instances (via adapter).
    /// </summary>
    public sealed class EngineRegistry
    {
        private readonly Dictionary<string, IAlgorithmEngine> _engines =
            new Dictionary<string, IAlgorithmEngine>(StringComparer.OrdinalIgnoreCase);

        public void Register(IAlgorithmEngine engine)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            _engines[engine.EngineId] = engine;
        }

        public void RegisterPlugin(IAlgorithmPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            Register(new PluginEngineAdapter(plugin));
        }

        public IAlgorithmEngine Resolve(string preferredEngineId)
        {
            if (!string.IsNullOrWhiteSpace(preferredEngineId) &&
                _engines.TryGetValue(preferredEngineId, out var engine) &&
                engine.IsAvailable)
            {
                return engine;
            }

            // Fallback: return first available engine
            foreach (var e in _engines.Values)
            {
                if (e.IsAvailable) return e;
            }

            return null;
        }

        public bool Unregister(string engineId)
        {
            if (string.IsNullOrWhiteSpace(engineId)) return false;
            return _engines.Remove(engineId);
        }

        public IReadOnlyList<AlgorithmEngineDescriptor> GetDescriptors()
        {
            var descriptors = new List<AlgorithmEngineDescriptor>();
            foreach (var engine in _engines.Values)
            {
                descriptors.Add(new AlgorithmEngineDescriptor
                {
                    EngineId = engine.EngineId,
                    EngineName = engine.EngineName,
                    EngineVersion = engine.EngineVersion,
                    IsAvailable = engine.IsAvailable,
                });
            }
            return descriptors;
        }
    }
}
