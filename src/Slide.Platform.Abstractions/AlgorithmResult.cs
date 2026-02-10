using System;
using System.Collections.Generic;

namespace Slide.Platform.Abstractions
{
    public sealed class AlgorithmResult
    {
        public string EngineId { get; set; }
        public string EngineVersion { get; set; }
        public AlgorithmExecutionStatus Status { get; set; }
        public bool IsOk { get; set; }
        public string DefectType { get; set; }
        public string Description { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<AlgorithmMeasurement> Measurements { get; set; } = new List<AlgorithmMeasurement>();
        public Dictionary<string, string> DebugInfo { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, byte[]> RenderImages { get; set; } = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    }
}
