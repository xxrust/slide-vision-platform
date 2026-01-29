using System;
using System.Collections.Generic;
using GlueInspect.Algorithm.Contracts;
using GlueInspect.Algorithm.ONNX;
using GlueInspect.Algorithm.OpenCV;
using WpfApp2.UI;

namespace WpfApp2.Algorithms
{
    public static class AlgorithmEngineRegistry
    {
        private static readonly Dictionary<string, IAlgorithmEngine> Engines = new Dictionary<string, IAlgorithmEngine>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        public static void Initialize(Page1 page1)
        {
            if (!_initialized)
            {
                Engines[AlgorithmEngineIds.OpenCv] = new OpenCvAlgorithmEngine();
                Engines[AlgorithmEngineIds.Onnx] = new OnnxAlgorithmEngine();
                _initialized = true;
                return;
            }
        }

        public static void EnsureInitialized(Page1 page1)
        {
            if (!_initialized)
            {
                Initialize(page1);
                return;
            }

        }

        public static IAlgorithmEngine ResolveEngine(string preferredEngineId)
        {
            EnsureInitialized(Page1.PageManager.Page1Instance);

            if (string.IsNullOrWhiteSpace(preferredEngineId))
            {
                preferredEngineId = AlgorithmEngineIds.OpenCv;
            }

            if (Engines.TryGetValue(preferredEngineId, out var preferredEngine) && preferredEngine != null)
            {
                if (preferredEngine.IsAvailable)
                {
                    return preferredEngine;
                }
            }

            return Engines.TryGetValue(AlgorithmEngineIds.OpenCv, out var openCvEngine) ? openCvEngine : preferredEngine;
        }

        public static IReadOnlyList<AlgorithmEngineDescriptor> GetDescriptors()
        {
            EnsureInitialized(Page1.PageManager.Page1Instance);

            var descriptors = new List<AlgorithmEngineDescriptor>();
            foreach (var engine in Engines.Values)
            {
                if (engine == null)
                {
                    continue;
                }

                descriptors.Add(new AlgorithmEngineDescriptor
                {
                    EngineId = engine.EngineId,
                    EngineName = engine.EngineName,
                    EngineVersion = engine.EngineVersion,
                    IsAvailable = engine.IsAvailable,
                    Description = GetDefaultDescription(engine.EngineId)
                });
            }

            return descriptors;
        }

        private static string GetDefaultDescription(string engineId)
        {
            if (string.Equals(engineId, AlgorithmEngineIds.OpenCv, StringComparison.OrdinalIgnoreCase))
            {
                return "OpenCV classic pipeline (incremental)";
            }

            if (string.Equals(engineId, AlgorithmEngineIds.Onnx, StringComparison.OrdinalIgnoreCase))
            {
                return "ONNX inference engine (model-based)";
            }

            return string.Empty;
        }
    }
}
