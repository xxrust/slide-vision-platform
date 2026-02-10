namespace Slide.Platform.Abstractions
{
    public sealed class AlgorithmEngineDescriptor
    {
        public string EngineId { get; set; }
        public string EngineName { get; set; }
        public string EngineVersion { get; set; }
        public bool IsAvailable { get; set; }
        public string Description { get; set; }
    }
}
