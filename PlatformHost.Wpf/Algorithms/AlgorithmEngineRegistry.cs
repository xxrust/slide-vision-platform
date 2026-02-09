using System;
using System.Collections.Generic;
using Slide.Algorithm.Contracts;
using Slide.Algorithm.ONNX;
using Slide.Algorithm.OpenCV;

namespace WpfApp2.Algorithms
{
    public static class AlgorithmEngineRegistry
    {
        private static readonly Dictionary<string, IAlgorithmEngine> Engines = new Dictionary<string, IAlgorithmEngine>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        public static void Initialize()
        {
            if (!_initialized)
            {
                var openCvEngine = new OpenCvAlgorithmEngine();
                var onnxEngine = new OnnxAlgorithmEngine();

                Engines[AlgorithmEngineIds.OpenCv] = openCvEngine;
                Engines[AlgorithmEngineIds.Onnx] = onnxEngine;
                Engines[AlgorithmEngineIds.OpenCvOnnx] = new OpenCvOnnxAlgorithmEngine(openCvEngine, onnxEngine);
                _initialized = true;
                return;
            }
        }

        public static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
                return;
            }

        }

        public static IAlgorithmEngine ResolveEngine(string preferredEngineId)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(preferredEngineId))
            {
                preferredEngineId = AlgorithmEngineIds.OpenCvOnnx;
            }

            if (string.Equals(preferredEngineId, AlgorithmEngineIds.OpenCv, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(preferredEngineId, AlgorithmEngineIds.Onnx, StringComparison.OrdinalIgnoreCase))
            {
                preferredEngineId = AlgorithmEngineIds.OpenCvOnnx;
            }

            if (Engines.TryGetValue(preferredEngineId, out var preferredEngine) && preferredEngine != null)
            {
                if (preferredEngine.IsAvailable)
                {
                    return preferredEngine;
                }
            }

            return Engines.TryGetValue(AlgorithmEngineIds.OpenCvOnnx, out var compositeEngine)
                ? compositeEngine
                : Engines.TryGetValue(AlgorithmEngineIds.OpenCv, out var openCvEngine) ? openCvEngine : preferredEngine;
        }

        public static IReadOnlyList<AlgorithmEngineDescriptor> GetDescriptors()
        {
            EnsureInitialized();

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
            if (string.Equals(engineId, AlgorithmEngineIds.OpenCvOnnx, StringComparison.OrdinalIgnoreCase))
            {
                return "OpenCV + ONNX pipeline (classic + deep learning)";
            }

            if (string.Equals(engineId, AlgorithmEngineIds.OpenCv, StringComparison.OrdinalIgnoreCase))
            {
                return "OpenCV classic pipeline (legacy)";
            }

            if (string.Equals(engineId, AlgorithmEngineIds.Onnx, StringComparison.OrdinalIgnoreCase))
            {
                return "ONNX inference engine (legacy)";
            }

            return string.Empty;
        }
    }
}
