using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GlueInspect.Algorithm.Contracts
{
    public static class AlgorithmEngineIds
    {
        public const string Vm = "VM";
        public const string OpenCv = "OpenCV";
        public const string Onnx = "ONNX";
    }

    public sealed class AlgorithmEngineDescriptor
    {
        public string EngineId { get; set; }
        public string EngineName { get; set; }
        public string EngineVersion { get; set; }
        public bool IsAvailable { get; set; }
        public string Description { get; set; }
    }

    public enum AlgorithmExecutionStatus
    {
        Unknown = 0,
        Success = 1,
        Failed = 2,
        NotAvailable = 3,
        Skipped = 4,
        LegacyPipeline = 5
    }

    public sealed class AlgorithmInput
    {
        public string TemplateName { get; set; }
        public string LotNumber { get; set; }
        public string ImageNumber { get; set; }
        public string SampleType { get; set; }
        public string CoatingType { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ImagePaths { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AlgorithmMeasurement
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string ValueText { get; set; }
        public bool HasValidData { get; set; }
        public double LowerLimit { get; set; }
        public double UpperLimit { get; set; }
        public bool IsOutOfRange { get; set; }
        public bool Is3DItem { get; set; }
        public int ToolIndex { get; set; }
    }

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
        public Dictionary<string, string> DebugInfo { get; set; } = new Dictionary<string, string>();
    }

    public interface IAlgorithmEngine
    {
        string EngineId { get; }
        string EngineName { get; }
        string EngineVersion { get; }
        bool IsAvailable { get; }
        Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken);
    }
}
