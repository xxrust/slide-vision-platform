using System;

namespace Slide.Platform.Abstractions
{
    public sealed class AlgorithmDescriptor
    {
        public AlgorithmDescriptor(string id, string name, Version version)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public string Id { get; }
        public string Name { get; }
        public Version Version { get; }
        public string Description { get; set; }
    }
}
