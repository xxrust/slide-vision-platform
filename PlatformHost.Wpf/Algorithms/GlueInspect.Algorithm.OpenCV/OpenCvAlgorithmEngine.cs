using System.Threading;
using System.Threading.Tasks;
using GlueInspect.Algorithm.Contracts;

namespace GlueInspect.Algorithm.OpenCV
{
    public sealed class OpenCvAlgorithmEngine : IAlgorithmEngine
    {
        public string EngineId => AlgorithmEngineIds.OpenCv;
        public string EngineName => "OpenCV";
        public string EngineVersion => "0.3";
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
