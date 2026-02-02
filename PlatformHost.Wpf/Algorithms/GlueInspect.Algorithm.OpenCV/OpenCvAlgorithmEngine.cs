using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GlueInspect.Algorithm.Contracts;
using OpenCvSharp;

namespace GlueInspect.Algorithm.OpenCV
{
    public sealed class OpenCvAlgorithmEngine : IAlgorithmEngine
    {
        private const string KeyImagePath = "\u56fe\u7247\u8def\u5f84";
        private const string KeyUseGray = "\u7070\u5ea6\u6a21\u5f0f";
        private const string KeyBlurKernel = "\u9ad8\u65af\u6838\u5c3a\u5bf8";
        private const string KeyBlurSigma = "\u9ad8\u65af\u03c3";
        private const string KeyCannyLow = "Canny\u4f4e\u9608\u503c";
        private const string KeyCannyHigh = "Canny\u9ad8\u9608\u503c";
        private const string KeyDilateIterations = "\u81a8\u80c0\u6b21\u6570";
        private const string KeyMeanMin = "\u7070\u5ea6\u5747\u503c\u4e0b\u9650";
        private const string KeyMeanMax = "\u7070\u5ea6\u5747\u503c\u4e0a\u9650";
        private const string KeyEdgeMin = "\u8fb9\u7f18\u50cf\u7d20\u6570\u4e0b\u9650";
        private const string KeyEdgeMax = "\u8fb9\u7f18\u50cf\u7d20\u6570\u4e0a\u9650";
        private const string KeyImageSourceCount = "ImageSourceCount";

        public string EngineId => AlgorithmEngineIds.OpenCv;
        public string EngineName => "OpenCV";
        public string EngineVersion => "1.0";
        public bool IsAvailable => true;

        public Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken)
        {
            var result = new AlgorithmResult
            {
                EngineId = EngineId,
                EngineVersion = EngineVersion,
                Status = AlgorithmExecutionStatus.Success
            };

            var parameters = input?.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int requiredCount = Math.Max(1, GetInt(parameters, KeyImageSourceCount, 1));
            var (primaryPath, secondaryPath) = ResolveInputPaths(input);

            if (string.IsNullOrWhiteSpace(primaryPath))
            {
                return Task.FromResult(Fail(result, "Missing image path."));
            }

            if (!File.Exists(primaryPath))
            {
                return Task.FromResult(Fail(result, $"Image not found: {primaryPath}"));
            }

            if (requiredCount >= 2 && string.IsNullOrWhiteSpace(secondaryPath))
            {
                return Task.FromResult(Fail(result, "Second image path is missing."));
            }

            if (!string.IsNullOrWhiteSpace(secondaryPath) && !File.Exists(secondaryPath))
            {
                return Task.FromResult(Fail(result, $"Image not found: {secondaryPath}"));
            }

            try
            {
                bool useGray = GetBool(parameters, KeyUseGray, true);
                int blurKernel = NormalizeKernel(GetInt(parameters, KeyBlurKernel, 5));
                double blurSigma = GetDouble(parameters, KeyBlurSigma, 1.2);
                double cannyLow = GetDouble(parameters, KeyCannyLow, 60);
                double cannyHigh = GetDouble(parameters, KeyCannyHigh, 120);
                int dilateIterations = Math.Max(0, GetInt(parameters, KeyDilateIterations, 1));

                double meanMin = GetDouble(parameters, KeyMeanMin, 60);
                double meanMax = GetDouble(parameters, KeyMeanMax, 200);
                double edgeMin = GetDouble(parameters, KeyEdgeMin, 500);
                double edgeMax = GetDouble(parameters, KeyEdgeMax, 20000);

                var primary = ProcessImage(primaryPath, useGray, blurKernel, blurSigma, cannyLow, cannyHigh, dilateIterations);
                ProcessedImage secondary = null;
                if (!string.IsNullOrWhiteSpace(secondaryPath))
                {
                    secondary = ProcessImage(secondaryPath, useGray, blurKernel, blurSigma, cannyLow, cannyHigh, dilateIterations);
                }

                using (var preprocess = CombineSideBySide(primary.Preprocess, secondary?.Preprocess))
                using (var edges = CombineSideBySide(primary.Edges, secondary?.Edges))
                using (var composite = CombineSideBySide(primary.Composite, secondary?.Composite))
                {
                    var preprocessBytes = EncodePng(preprocess);
                    if (preprocessBytes != null && preprocessBytes.Length > 0)
                    {
                        result.RenderImages["Render.Preprocess"] = preprocessBytes;
                    }

                    var edgeBytes = EncodePng(edges);
                    if (edgeBytes != null && edgeBytes.Length > 0)
                    {
                        result.RenderImages["Render.Edge"] = edgeBytes;
                    }

                    var compositeBytes = EncodePng(composite);
                    if (compositeBytes != null && compositeBytes.Length > 0)
                    {
                        result.RenderImages["Render.Composite"] = compositeBytes;
                    }
                }

                result.DebugInfo["Render.Input"] = primaryPath;
                result.DebugInfo["Render.Preprocess"] = "[memory]";
                result.DebugInfo["Render.Edge"] = "[memory]";
                result.DebugInfo["Render.Composite"] = "[memory]";

                try
                {
                    var inputBytes = File.ReadAllBytes(primaryPath);
                    if (inputBytes != null && inputBytes.Length > 0)
                    {
                        result.RenderImages["Render.Input"] = inputBytes;
                    }
                }
                catch
                {
                    // Ignore input read failures
                }

                var measurements = new List<AlgorithmMeasurement>();
                int toolIndex = 1;
                string name1 = GetSourceName(parameters, 1, "Image1");
                AddMeasurements(measurements, name1, primary, meanMin, meanMax, edgeMin, edgeMax, ref toolIndex);
                if (secondary != null)
                {
                    string name2 = GetSourceName(parameters, 2, "Image2");
                    AddMeasurements(measurements, name2, secondary, meanMin, meanMax, edgeMin, edgeMax, ref toolIndex);
                }

                bool hasNg = measurements.Exists(m => m.IsOutOfRange);

                result.Measurements = measurements;
                result.IsOk = !hasNg;
                result.DefectType = hasNg ? "NG" : "OK";
                result.Description = "OpenCV pipeline output";

                primary.Dispose();
                secondary?.Dispose();
            }
            catch (Exception ex)
            {
                return Task.FromResult(Fail(result, ex.Message));
            }

            return Task.FromResult(result);
        }

        private static (string, string) ResolveInputPaths(AlgorithmInput input)
        {
            if (input?.ImagePaths != null)
            {
                string primary = null;
                string secondary = null;

                if (input.ImagePaths.TryGetValue("Image1", out var image1) && !string.IsNullOrWhiteSpace(image1))
                {
                    primary = image1;
                }

                if (input.ImagePaths.TryGetValue("Image2", out var image2) && !string.IsNullOrWhiteSpace(image2))
                {
                    if (string.IsNullOrWhiteSpace(primary))
                    {
                        primary = image2;
                    }
                    else
                    {
                        secondary = image2;
                    }
                }

                return (primary, secondary);
            }

            if (input?.Parameters != null && input.Parameters.TryGetValue(KeyImagePath, out var paramPath))
            {
                return (paramPath, null);
            }

            return (string.Empty, null);
        }

        private static AlgorithmResult Fail(AlgorithmResult result, string message)
        {
            result.Status = AlgorithmExecutionStatus.Failed;
            result.IsOk = false;
            result.DefectType = "ERROR";
            result.ErrorMessage = message;
            result.Description = message;
            return result;
        }

        private sealed class ProcessedImage
        {
            public Mat Preprocess { get; set; }
            public Mat Edges { get; set; }
            public Mat Composite { get; set; }
            public double Mean { get; set; }
            public double Std { get; set; }
            public double EdgeCount { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public void Dispose()
            {
                Preprocess?.Dispose();
                Edges?.Dispose();
                Composite?.Dispose();
            }
        }

        private static byte[] EncodePng(Mat mat)
        {
            if (mat == null || mat.Empty())
            {
                return null;
            }

            if (TryEncode(mat, ".png", out var bytes))
            {
                return bytes;
            }

            Mat converted = null;
            Mat bgr = null;

            try
            {
                Mat working = mat;
                if (mat.Depth() != MatType.CV_8U)
                {
                    converted = new Mat();
                    mat.ConvertTo(converted, MatType.CV_8U);
                    working = converted;
                }

                if (working.Channels() == 1)
                {
                    bgr = new Mat();
                    Cv2.CvtColor(working, bgr, ColorConversionCodes.GRAY2BGR);
                }
                else if (working.Channels() == 4)
                {
                    bgr = new Mat();
                    Cv2.CvtColor(working, bgr, ColorConversionCodes.BGRA2BGR);
                }

                if (bgr != null && TryEncode(bgr, ".png", out bytes))
                {
                    return bytes;
                }

                if (TryEncode(working, ".bmp", out bytes))
                {
                    return bytes;
                }

                if (bgr != null && TryEncode(bgr, ".bmp", out bytes))
                {
                    return bytes;
                }
            }
            catch
            {
                // Ignore encode fallback failures
            }
            finally
            {
                bgr?.Dispose();
                converted?.Dispose();
            }

            return null;
        }

        private static bool TryEncode(Mat mat, string ext, out byte[] bytes)
        {
            bytes = null;
            if (mat == null || mat.Empty())
            {
                return false;
            }

            try
            {
                if (Cv2.ImEncode(ext, mat, out bytes) && bytes != null && bytes.Length > 0)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore encode failures
            }

            bytes = null;
            return false;
        }

        private static ProcessedImage ProcessImage(string imagePath, bool useGray, int blurKernel, double blurSigma, double cannyLow, double cannyHigh, int dilateIterations)
        {
            using (var src = Cv2.ImRead(imagePath, ImreadModes.Color))
            {
                if (src.Empty())
                {
                    throw new InvalidOperationException($"Failed to load image: {imagePath}");
                }

                var gray = new Mat();
                var blurred = new Mat();
                var edges = new Mat();

                if (useGray)
                {
                    Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.GaussianBlur(gray, blurred, new Size(blurKernel, blurKernel), blurSigma);
                }
                else
                {
                    Cv2.GaussianBlur(src, blurred, new Size(blurKernel, blurKernel), blurSigma);
                    Cv2.CvtColor(blurred, gray, ColorConversionCodes.BGR2GRAY);
                }

                Cv2.Canny(gray, edges, cannyLow, cannyHigh);

                if (dilateIterations > 0)
                {
                    Cv2.Dilate(edges, edges, null, iterations: dilateIterations);
                }

                var composite = BuildComposite(src, edges);

                Cv2.MeanStdDev(gray, out var mean, out var stddev);
                double meanValue = mean.Val0;
                double stdValue = stddev.Val0;
                double edgeCount = Cv2.CountNonZero(edges);

                gray.Dispose();

                return new ProcessedImage
                {
                    Preprocess = blurred,
                    Edges = edges,
                    Composite = composite,
                    Mean = meanValue,
                    Std = stdValue,
                    EdgeCount = edgeCount,
                    Width = src.Width,
                    Height = src.Height
                };
            }
        }

        private static Mat BuildComposite(Mat src, Mat edges)
        {
            var mask = new Mat();
            Cv2.CvtColor(edges, mask, ColorConversionCodes.GRAY2BGR);
            var channels = mask.Split();
            channels[0].SetTo(0);
            channels[1].SetTo(0);
            Cv2.Merge(channels, mask);
            foreach (var ch in channels)
            {
                ch.Dispose();
            }

            var composite = new Mat();
            Cv2.AddWeighted(src, 0.8, mask, 0.8, 0, composite);
            mask.Dispose();
            return composite;
        }

        private static Mat CombineSideBySide(Mat left, Mat right)
        {
            if (left == null && right == null)
            {
                return new Mat();
            }

            if (right == null)
            {
                return left.Clone();
            }

            if (left == null)
            {
                return right.Clone();
            }

            int targetHeight = Math.Max(left.Rows, right.Rows);
            using (var leftResized = ResizeToHeight(left, targetHeight))
            using (var rightResized = ResizeToHeight(right, targetHeight))
            {
                var combined = new Mat();
                Cv2.HConcat(new[] { leftResized, rightResized }, combined);
                return combined;
            }
        }

        private static Mat ResizeToHeight(Mat source, int targetHeight)
        {
            if (source.Rows == targetHeight)
            {
                return source.Clone();
            }

            int targetWidth = (int)Math.Round(source.Cols * targetHeight / (double)source.Rows);
            var resized = new Mat();
            Cv2.Resize(source, resized, new Size(targetWidth, targetHeight), 0, 0, InterpolationFlags.Area);
            return resized;
        }

        private static string GetSourceName(IDictionary<string, string> parameters, int index, string fallback)
        {
            if (parameters != null && parameters.TryGetValue($"ImageSourceName{index}", out var name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return fallback;
        }

        private static void AddMeasurements(List<AlgorithmMeasurement> measurements, string label, ProcessedImage image, double meanMin, double meanMax, double edgeMin, double edgeMax, ref int toolIndex)
        {
            measurements.Add(BuildMeasurement($"{label}-Mean", image.Mean, meanMin, meanMax, toolIndex++));
            measurements.Add(BuildMeasurement($"{label}-Std", image.Std, double.MinValue, double.MaxValue, toolIndex++));
            measurements.Add(BuildMeasurement($"{label}-EdgeCount", image.EdgeCount, edgeMin, edgeMax, toolIndex++));
            measurements.Add(BuildMeasurement($"{label}-Width", image.Width, double.MinValue, double.MaxValue, toolIndex++));
            measurements.Add(BuildMeasurement($"{label}-Height", image.Height, double.MinValue, double.MaxValue, toolIndex++));
        }

        private static AlgorithmMeasurement BuildMeasurement(string name, double value, double lower, double upper, int toolIndex)
        {
            bool hasLimit = lower != double.MinValue || upper != double.MaxValue;
            bool outOfRange = hasLimit && (value < lower || value > upper);

            return new AlgorithmMeasurement
            {
                Name = name,
                Value = value,
                ValueText = value.ToString("F3"),
                HasValidData = true,
                LowerLimit = lower,
                UpperLimit = upper,
                IsOutOfRange = outOfRange,
                ToolIndex = toolIndex
            };
        }

        private static int NormalizeKernel(int kernel)
        {
            if (kernel < 1)
            {
                return 3;
            }

            return kernel % 2 == 0 ? kernel + 1 : kernel;
        }

        private static bool GetBool(IDictionary<string, string> parameters, string key, bool fallback)
        {
            if (parameters != null && parameters.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value))
            {
                return value;
            }

            return fallback;
        }

        private static int GetInt(IDictionary<string, string> parameters, string key, int fallback)
        {
            if (parameters != null && parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value))
            {
                return value;
            }

            return fallback;
        }

        private static double GetDouble(IDictionary<string, string> parameters, string key, double fallback)
        {
            if (parameters != null && parameters.TryGetValue(key, out var raw) && double.TryParse(raw, out var value))
            {
                return value;
            }

            return fallback;
        }
    }
}
