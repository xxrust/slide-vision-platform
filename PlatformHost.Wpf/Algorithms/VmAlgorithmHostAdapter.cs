using System;
using System.Threading;
using System.Threading.Tasks;
using GlueInspect.Algorithm.Contracts;
using GlueInspect.Algorithm.VM;
using WpfApp2.UI;

namespace WpfApp2.Algorithms
{
    internal sealed class VmAlgorithmHostAdapter : IVmAlgorithmHost
    {
        private readonly Page1 _page1;

        public VmAlgorithmHostAdapter(Page1 page1)
        {
            _page1 = page1;
        }

        public bool IsAvailable => _page1 != null;
        public string HostVersion => "legacy";

        public Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken)
        {
            if (_page1 == null)
            {
                return Task.FromResult(new AlgorithmResult
                {
                    EngineId = AlgorithmEngineIds.Vm,
                    EngineVersion = HostVersion,
                    Status = AlgorithmExecutionStatus.NotAvailable,
                    ErrorMessage = "Page1 instance is not available."
                });
            }

            var tcs = new TaskCompletionSource<AlgorithmResult>();
            EventHandler<AlgorithmResultEventArgs> handler = null;
            handler = (sender, args) =>
            {
                _page1.AlgorithmResultProduced -= handler;
                tcs.TrySetResult(args.Result);
            };

            _page1.AlgorithmResultProduced += handler;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    _page1.AlgorithmResultProduced -= handler;
                    tcs.TrySetCanceled();
                });
            }

            return tcs.Task;
        }
    }
}
