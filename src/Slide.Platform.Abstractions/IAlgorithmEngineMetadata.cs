using System.Collections.Generic;

namespace Slide.Platform.Abstractions
{
    public sealed class AlgorithmParameterSchema
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string DataType { get; set; } = "string";
        public string DefaultValue { get; set; }
        public bool IsRequired { get; set; }
        public string Group { get; set; }
    }

    public sealed class AlgorithmOutputSchema
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public bool HasLimits { get; set; }
    }

    /// <summary>
    /// Optional interface: engine self-describes its parameter and output schema.
    /// Engines that do not implement this interface remain fully functional.
    /// </summary>
    public interface IAlgorithmEngineMetadata
    {
        IReadOnlyList<AlgorithmParameterSchema> GetParameterSchema();
        IReadOnlyList<AlgorithmOutputSchema> GetOutputSchema();
    }
}
