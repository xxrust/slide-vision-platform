using System;

namespace Slide.Platform.Abstractions
{
    public interface IAlgorithmSession : IDisposable
    {
        SimpleAlgorithmResult Run(SimpleAlgorithmInput input);
    }
}
