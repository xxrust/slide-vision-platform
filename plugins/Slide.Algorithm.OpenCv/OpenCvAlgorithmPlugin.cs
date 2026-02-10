using System;
using System.IO;
using Slide.Platform.Abstractions;
using OpenCvSharp;

namespace Slide.Algorithm.OpenCv
{
    public sealed class OpenCvAlgorithmPlugin : IAlgorithmPlugin
    {
        private static readonly AlgorithmDescriptor DescriptorInstance = new AlgorithmDescriptor(
            id: "opencv.basic",
            name: "OpenCV Basic Algorithm",
            version: new Version(1, 0, 0))
        {
            Description = "Use OpenCV to compute mean/std of grayscale image."
        };

        public AlgorithmDescriptor Descriptor => DescriptorInstance;

        public IAlgorithmSession CreateSession()
        {
            return new OpenCvAlgorithmSession();
        }

        private sealed class OpenCvAlgorithmSession : IAlgorithmSession
        {
            public SimpleAlgorithmResult Run(SimpleAlgorithmInput input)
            {
                var result = new SimpleAlgorithmResult();
                if (input == null || string.IsNullOrWhiteSpace(input.ImagePath))
                {
                    result.Success = false;
                    result.Message = "未提供图像路径";
                    return result;
                }

                if (!File.Exists(input.ImagePath))
                {
                    result.Success = false;
                    result.Message = $"图像不存在: {input.ImagePath}";
                    return result;
                }

                using (var mat = Cv2.ImRead(input.ImagePath, ImreadModes.Grayscale))
                {
                    if (mat.Empty())
                    {
                        result.Success = false;
                        result.Message = "读取图像失败";
                        return result;
                    }

                    Cv2.MeanStdDev(mat, out var mean, out var stddev);

                    result.Success = true;
                    result.Message = "OK";
                    result.Metrics["Mean"] = Math.Round(mean.Val0, 4);
                    result.Metrics["Std"] = Math.Round(stddev.Val0, 4);
                    result.Metrics["Width"] = mat.Width;
                    result.Metrics["Height"] = mat.Height;
                }

                return result;
            }

            public void Dispose()
            {
            }
        }
    }
}
