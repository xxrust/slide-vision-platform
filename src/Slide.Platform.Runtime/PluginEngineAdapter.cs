using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Slide.Platform.Abstractions;

namespace Slide.Platform.Runtime
{
    /// <summary>
    /// Bridges an old IAlgorithmPlugin into the unified IAlgorithmEngine interface.
    /// </summary>
    public sealed class PluginEngineAdapter : IAlgorithmEngine
    {
        private readonly IAlgorithmPlugin _plugin;

        public PluginEngineAdapter(IAlgorithmPlugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        public string EngineId => "Plugin:" + (_plugin.Descriptor?.Id ?? "unknown");
        public string EngineName => _plugin.Descriptor?.Name ?? "Unknown Plugin";
        public string EngineVersion => _plugin.Descriptor?.Version?.ToString() ?? "0.0.0";
        public bool IsAvailable => true;

        public Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken)
        {
            var simpleInput = new SimpleAlgorithmInput();

            // Take the first image path if available
            if (input.ImagePaths != null && input.ImagePaths.Count > 0)
            {
                simpleInput.ImagePath = input.ImagePaths.Values.FirstOrDefault();
            }

            // Copy parameters
            if (input.Parameters != null)
            {
                foreach (var kvp in input.Parameters)
                {
                    simpleInput.Parameters[kvp.Key] = kvp.Value;
                }
            }

            SimpleAlgorithmResult simpleResult;
            using (var session = _plugin.CreateSession())
            {
                simpleResult = session.Run(simpleInput);
            }

            var result = new AlgorithmResult
            {
                EngineId = EngineId,
                EngineVersion = EngineVersion,
                Status = simpleResult.Success ? AlgorithmExecutionStatus.Success : AlgorithmExecutionStatus.Failed,
                IsOk = simpleResult.Success,
                Description = simpleResult.Message,
                ErrorMessage = simpleResult.Success ? null : simpleResult.Message,
            };

            // Convert Metrics to Measurements
            if (simpleResult.Metrics != null)
            {
                foreach (var kvp in simpleResult.Metrics)
                {
                    result.Measurements.Add(new AlgorithmMeasurement
                    {
                        Name = kvp.Key,
                        Value = kvp.Value,
                        ValueText = kvp.Value.ToString(),
                        HasValidData = true,
                    });
                }
            }

            // Convert Tags to DebugInfo
            if (simpleResult.Tags != null)
            {
                foreach (var kvp in simpleResult.Tags)
                {
                    result.DebugInfo[kvp.Key] = kvp.Value;
                }
            }

            return Task.FromResult(result);
        }
    }
}
