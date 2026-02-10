namespace Slide.Platform.Abstractions
{
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
}
