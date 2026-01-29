using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlueInspect.Platform.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace GlueInspect.Algorithm.Onnx
{
    public sealed class OnnxAlgorithmPlugin : IAlgorithmPlugin
    {
        private static readonly AlgorithmDescriptor DescriptorInstance = new AlgorithmDescriptor(
            id: "onnx.identity",
            name: "ONNX Identity Algorithm",
            version: new Version(1, 0, 0))
        {
            Description = "Run a simple identity ONNX model and report output stats."
        };

        public AlgorithmDescriptor Descriptor => DescriptorInstance;

        public IAlgorithmSession CreateSession()
        {
            return new OnnxAlgorithmSession();
        }

        private sealed class OnnxAlgorithmSession : IAlgorithmSession
        {
            private InferenceSession _session;

            public AlgorithmResult Run(AlgorithmInput input)
            {
                var result = new AlgorithmResult();
                var modelPath = ResolveModelPath(input);
                if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                {
                    result.Success = false;
                    result.Message = "未找到ONNX模型文件";
                    return result;
                }

                EnsureSession(modelPath);

                var tensor = new DenseTensor<float>(new[] { 1, 3 });
                tensor[0, 0] = 0.1f;
                tensor[0, 1] = 0.5f;
                tensor[0, 2] = 0.9f;

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", tensor)
                };

                using (var outputs = _session.Run(inputs))
                {
                    var outputTensor = outputs.First().AsTensor<float>();
                    var data = outputTensor.ToArray();
                    var sum = 0.0;
                    for (var i = 0; i < data.Length; i++)
                    {
                        sum += data[i];
                    }

                    var mean = sum / data.Length;
                    result.Success = true;
                    result.Message = "OK";
                    result.Metrics["OutputCount"] = data.Length;
                    result.Metrics["OutputMean"] = Math.Round(mean, 6);
                }

                return result;
            }

            private static string ResolveModelPath(AlgorithmInput input)
            {
                if (input?.Parameters != null && input.Parameters.TryGetValue("ModelPath", out var pathObj))
                {
                    return pathObj?.ToString();
                }

                return null;
            }

            private void EnsureSession(string modelPath)
            {
                if (_session != null)
                {
                    return;
                }

                _session = new InferenceSession(modelPath);
            }

            public void Dispose()
            {
                _session?.Dispose();
                _session = null;
            }
        }
    }
}
