using System;
using System.Threading;
using System.Threading.Tasks;
using Slide.Algorithm.Contracts;

namespace WpfApp2.Algorithms
{
    public sealed class OpenCvOnnxAlgorithmEngine : IAlgorithmEngine
    {
        private readonly IAlgorithmEngine _openCv;
        private readonly IAlgorithmEngine _onnx;

        public OpenCvOnnxAlgorithmEngine(IAlgorithmEngine openCv, IAlgorithmEngine onnx)
        {
            _openCv = openCv;
            _onnx = onnx;
        }

        public string EngineId => AlgorithmEngineIds.OpenCvOnnx;

        public string EngineName => "OpenCV + ONNX";

        public string EngineVersion
        {
            get
            {
                string openCvVersion = _openCv?.EngineVersion ?? "n/a";
                string onnxVersion = _onnx?.EngineVersion ?? "n/a";
                return $"{openCvVersion}+{onnxVersion}";
            }
        }

        public bool IsAvailable => (_openCv?.IsAvailable ?? false) || (_onnx?.IsAvailable ?? false);

        public async Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken)
        {
            if (!IsAvailable)
            {
                return new AlgorithmResult
                {
                    EngineId = EngineId,
                    EngineVersion = EngineVersion,
                    Status = AlgorithmExecutionStatus.NotAvailable,
                    IsOk = false,
                    DefectType = "引擎不可用",
                    Description = "OpenCV/ONNX 未就绪"
                };
            }

            Task<AlgorithmResult> openCvTask = ExecuteEngineAsync(_openCv, input, cancellationToken);
            Task<AlgorithmResult> onnxTask = ExecuteEngineAsync(_onnx, input, cancellationToken);

            AlgorithmResult openCvResult = await openCvTask.ConfigureAwait(false);
            AlgorithmResult onnxResult = await onnxTask.ConfigureAwait(false);

            return MergeResults(openCvResult, onnxResult);
        }

        private static async Task<AlgorithmResult> ExecuteEngineAsync(IAlgorithmEngine engine, AlgorithmInput input, CancellationToken cancellationToken)
        {
            if (engine == null || !engine.IsAvailable)
            {
                return null;
            }

            try
            {
                return await engine.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new AlgorithmResult
                {
                    EngineId = engine.EngineId,
                    EngineVersion = engine.EngineVersion,
                    Status = AlgorithmExecutionStatus.Failed,
                    IsOk = false,
                    DefectType = "异常",
                    Description = "算法执行异常",
                    ErrorMessage = ex.Message
                };
            }
        }

        private AlgorithmResult MergeResults(AlgorithmResult openCvResult, AlgorithmResult onnxResult)
        {
            var result = new AlgorithmResult
            {
                EngineId = EngineId,
                EngineVersion = EngineVersion,
                Status = ResolveStatus(openCvResult, onnxResult),
                IsOk = ResolveIsOk(openCvResult, onnxResult),
                DefectType = ResolveDefectType(openCvResult, onnxResult),
                Description = BuildDescription(openCvResult, onnxResult)
            };

            if (openCvResult?.Measurements != null && openCvResult.Measurements.Count > 0)
            {
                result.Measurements.AddRange(openCvResult.Measurements);
            }
            else if (onnxResult?.Measurements != null && onnxResult.Measurements.Count > 0)
            {
                result.Measurements.AddRange(onnxResult.Measurements);
            }

            AppendDebugInfo(result, "OpenCV", openCvResult);
            AppendDebugInfo(result, "ONNX", onnxResult);
            AppendRenderImages(result, "OpenCV", openCvResult);
            AppendRenderImages(result, "ONNX", onnxResult);

            return result;
        }

        private static AlgorithmExecutionStatus ResolveStatus(AlgorithmResult openCvResult, AlgorithmResult onnxResult)
        {
            if (openCvResult?.Status == AlgorithmExecutionStatus.Failed || onnxResult?.Status == AlgorithmExecutionStatus.Failed)
            {
                return AlgorithmExecutionStatus.Failed;
            }

            bool openCvUnavailable = openCvResult == null || openCvResult.Status == AlgorithmExecutionStatus.NotAvailable;
            bool onnxUnavailable = onnxResult == null || onnxResult.Status == AlgorithmExecutionStatus.NotAvailable;

            if (openCvUnavailable && onnxUnavailable)
            {
                return AlgorithmExecutionStatus.NotAvailable;
            }

            if (openCvResult?.Status == AlgorithmExecutionStatus.Success || onnxResult?.Status == AlgorithmExecutionStatus.Success)
            {
                return AlgorithmExecutionStatus.Success;
            }

            return AlgorithmExecutionStatus.Unknown;
        }

        private static bool ResolveIsOk(AlgorithmResult openCvResult, AlgorithmResult onnxResult)
        {
            if (onnxResult != null)
            {
                return onnxResult.IsOk;
            }

            if (openCvResult != null)
            {
                return openCvResult.IsOk;
            }

            return false;
        }

        private static string ResolveDefectType(AlgorithmResult openCvResult, AlgorithmResult onnxResult)
        {
            if (!string.IsNullOrWhiteSpace(onnxResult?.DefectType))
            {
                return onnxResult.DefectType;
            }

            if (!string.IsNullOrWhiteSpace(openCvResult?.DefectType))
            {
                return openCvResult.DefectType;
            }

            return "良品";
        }

        private static string BuildDescription(AlgorithmResult openCvResult, AlgorithmResult onnxResult)
        {
            string openCvDesc = openCvResult?.Description;
            string onnxDesc = onnxResult?.Description;

            if (string.IsNullOrWhiteSpace(openCvDesc) && string.IsNullOrWhiteSpace(onnxDesc))
            {
                return "OpenCV+ONNX 组合执行";
            }

            if (string.IsNullOrWhiteSpace(openCvDesc))
            {
                return onnxDesc;
            }

            if (string.IsNullOrWhiteSpace(onnxDesc))
            {
                return openCvDesc;
            }

            return $"{openCvDesc} | {onnxDesc}";
        }

        private static void AppendDebugInfo(AlgorithmResult target, string prefix, AlgorithmResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(source.EngineId))
            {
                target.DebugInfo[$"{prefix}.EngineId"] = source.EngineId;
            }

            target.DebugInfo[$"{prefix}.Status"] = source.Status.ToString();

            if (!string.IsNullOrWhiteSpace(source.ErrorMessage))
            {
                target.DebugInfo[$"{prefix}.Error"] = source.ErrorMessage;
            }

            if (source.DebugInfo == null)
            {
                return;
            }

            foreach (var entry in source.DebugInfo)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                target.DebugInfo[$"{prefix}.{entry.Key}"] = entry.Value ?? string.Empty;
            }
        }

        private static void AppendRenderImages(AlgorithmResult target, string prefix, AlgorithmResult source)
        {
            if (target == null || source?.RenderImages == null)
            {
                return;
            }

            foreach (var entry in source.RenderImages)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                if (entry.Value == null || entry.Value.Length == 0)
                {
                    continue;
                }

                target.RenderImages[$"{prefix}.{entry.Key}"] = entry.Value;
            }
        }
    }
}
