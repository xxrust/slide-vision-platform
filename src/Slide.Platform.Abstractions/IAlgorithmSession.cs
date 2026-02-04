using System;

namespace Slide.Platform.Abstractions
{
    public interface IAlgorithmSession : IDisposable
    {
        AlgorithmResult Run(AlgorithmInput input);
    }
}
