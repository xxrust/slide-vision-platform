using System.Collections.Generic;

namespace Slide.Platform.Abstractions
{
    public sealed class SimpleAlgorithmResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public IDictionary<string, double> Metrics { get; } = new Dictionary<string, double>();
        public IDictionary<string, string> Tags { get; } = new Dictionary<string, string>();
    }
}
