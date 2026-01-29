using System.Collections.Generic;
using GlueInspect.Platform.Abstractions;

namespace GlueInspect.Platform.Runtime
{
    public sealed class PluginLoadResult
    {
        public IList<IAlgorithmPlugin> Plugins { get; } = new List<IAlgorithmPlugin>();
        public IList<string> Errors { get; } = new List<string>();
    }
}
