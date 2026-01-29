using System.Collections.Generic;

namespace GlueInspect.Platform.Abstractions
{
    public sealed class AlgorithmInput
    {
        public string ImagePath { get; set; }
        public byte[] ImageBytes { get; set; }
        public IDictionary<string, object> Parameters { get; } = new Dictionary<string, object>();
    }
}
