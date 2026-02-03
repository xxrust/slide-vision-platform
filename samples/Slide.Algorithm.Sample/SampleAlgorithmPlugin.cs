using System;
using System.IO;
using Slide.Platform.Abstractions;

namespace Slide.Algorithm.Sample
{
    public sealed class SampleAlgorithmPlugin : IAlgorithmPlugin
    {
        private static readonly AlgorithmDescriptor DescriptorInstance = new AlgorithmDescriptor(
            id: "sample.basic",
            name: "Sample Basic Algorithm",
            version: new Version(1, 0, 0))
        {
            Description = "Example plugin that outputs simple numeric metrics from bytes."
        };

        public AlgorithmDescriptor Descriptor => DescriptorInstance;

        public IAlgorithmSession CreateSession()
        {
            return new SampleAlgorithmSession();
        }

        private sealed class SampleAlgorithmSession : IAlgorithmSession
        {
            public AlgorithmResult Run(AlgorithmInput input)
            {
                var result = new AlgorithmResult { Success = true };
                var bytes = ResolveBytes(input);

                if (bytes.Length == 0)
                {
                    result.Success = false;
                    result.Message = "未提供有效的图像数据";
                    return result;
                }

                double sum = 0;
                for (var i = 0; i < bytes.Length; i++)
                {
                    sum += bytes[i];
                }

                var avg = sum / bytes.Length;
                double varianceSum = 0;
                for (var i = 0; i < bytes.Length; i++)
                {
                    var diff = bytes[i] - avg;
                    varianceSum += diff * diff;
                }

                var std = Math.Sqrt(varianceSum / bytes.Length);

                result.Metrics["ByteCount"] = bytes.Length;
                result.Metrics["MeanByte"] = Math.Round(avg, 4);
                result.Metrics["StdByte"] = Math.Round(std, 4);
                result.Metrics["SizeKB"] = Math.Round(bytes.Length / 1024.0, 3);
                result.Message = "OK";
                return result;
            }

            private static byte[] ResolveBytes(AlgorithmInput input)
            {
                if (input?.ImageBytes != null && input.ImageBytes.Length > 0)
                {
                    return input.ImageBytes;
                }

                if (!string.IsNullOrWhiteSpace(input?.ImagePath) && File.Exists(input.ImagePath))
                {
                    return File.ReadAllBytes(input.ImagePath);
                }

                return Array.Empty<byte>();
            }

            public void Dispose()
            {
            }
        }
    }
}
