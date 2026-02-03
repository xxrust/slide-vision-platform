namespace Slide.Platform.Abstractions
{
    public interface IAlgorithmPlugin
    {
        AlgorithmDescriptor Descriptor { get; }
        IAlgorithmSession CreateSession();
    }
}
