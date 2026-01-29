using System;

namespace GlueInspect.Platform.Abstractions
{
    public interface IAlgorithmSession : IDisposable
    {
        AlgorithmResult Run(AlgorithmInput input);
    }
}
