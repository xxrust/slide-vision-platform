using System;
using Slide.Platform.Abstractions;

namespace WpfApp2.Algorithms
{
    public sealed class AlgorithmResultEventArgs : EventArgs
    {
        public AlgorithmResultEventArgs(AlgorithmResult result)
        {
            Result = result;
        }

        public AlgorithmResult Result { get; }
    }
}
