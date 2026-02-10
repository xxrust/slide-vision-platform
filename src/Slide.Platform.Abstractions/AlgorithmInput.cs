using System.Collections.Generic;

namespace Slide.Platform.Abstractions
{
    public sealed class AlgorithmInput
    {
        public string TemplateName { get; set; }
        public string LotNumber { get; set; }
        public string ImageNumber { get; set; }
        public string TemplateProfileId { get; set; }
        public string TemplateProfileName { get; set; }
        public string SampleType { get; set; }
        public string CoatingType { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ImagePaths { get; set; } = new Dictionary<string, string>();
    }
}
