using System.Threading;
using System.Threading.Tasks;
using Slide.Algorithm.Contracts;

namespace Slide.Algorithm.ONNX
{
    public sealed class OnnxAlgorithmEngine : IAlgorithmEngine
    {
        public string EngineId => AlgorithmEngineIds.Onnx;
        public string EngineName => "ONNX Runtime";
        public string EngineVersion => "0.1";
        public bool IsAvailable => true;

        public Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AlgorithmResult
            {
                EngineId = EngineId,
                EngineVersion = EngineVersion,
                Status = AlgorithmExecutionStatus.Success,
                IsOk = true,
                DefectType = "良品",
                Description = "参数对齐占位输出"
            });
        }
    }
}
