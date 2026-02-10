using System.Threading;
using System.Threading.Tasks;

namespace Slide.Platform.Abstractions
{
    public interface IAlgorithmEngine
    {
        string EngineId { get; }
        string EngineName { get; }
        string EngineVersion { get; }
        bool IsAvailable { get; }
        Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken);
    }
}
