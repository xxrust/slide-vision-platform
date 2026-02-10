using System.Threading;
using System.Threading.Tasks;
using Slide.Platform.Abstractions;

namespace Slide.Algorithm.VM
{
    public interface IVmAlgorithmHost
    {
        bool IsAvailable { get; }
        string HostVersion { get; }
        Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken);
    }

    public sealed class VmAlgorithmEngine : IAlgorithmEngine
    {
        private IVmAlgorithmHost _host;

        public VmAlgorithmEngine(IVmAlgorithmHost host)
        {
            _host = host;
        }

        public string EngineId => AlgorithmEngineIds.Vm;
        public string EngineName => "VisionMaster";
        public string EngineVersion => _host?.HostVersion ?? "unknown";
        public bool IsAvailable => _host != null && _host.IsAvailable;

        public void SetHost(IVmAlgorithmHost host)
        {
            _host = host;
        }

        public Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken)
        {
            if (!IsAvailable)
            {
                return Task.FromResult(new AlgorithmResult
                {
                    EngineId = EngineId,
                    EngineVersion = EngineVersion,
                    Status = AlgorithmExecutionStatus.NotAvailable,
                    ErrorMessage = "VM host is not available."
                });
            }

            return _host.ExecuteAsync(input, cancellationToken);
        }
    }
}
