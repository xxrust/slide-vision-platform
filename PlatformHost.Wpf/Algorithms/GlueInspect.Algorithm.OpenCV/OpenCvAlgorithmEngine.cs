using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GlueInspect.Algorithm.Contracts;

namespace GlueInspect.Algorithm.OpenCV
{
    public sealed class OpenCvAlgorithmEngine : IAlgorithmEngine
    {
        public string EngineId => AlgorithmEngineIds.OpenCv;
        public string EngineName => "OpenCV";
        public string EngineVersion => "0.3";
        public bool IsAvailable => true;

        public Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken cancellationToken)
        {
            var profileId = GetStringParameter(input, "TemplateProfileId");
            var sampleType = GetStringParameter(input, "SampleType");

            if (string.Equals(profileId, "profile-basic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sampleType, "Demo", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(BuildDemoResult(input));
            }

            return Task.FromResult(new AlgorithmResult
            {
                EngineId = EngineId,
                EngineVersion = EngineVersion,
                Status = AlgorithmExecutionStatus.Success,
                IsOk = true,
                DefectType = "鑹搧",
                Description = "鍙傛暟瀵归綈鍗犱綅杈撳嚭"
            });
        }

        private static string GetStringParameter(AlgorithmInput input, string key)
        {
            if (input?.Parameters == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (input.Parameters.TryGetValue(key, out var value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }

        private AlgorithmResult BuildDemoResult(AlgorithmInput input)
        {
            var parameters = input?.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            double length = GetDouble(parameters, "鍩哄噯闀垮害");
            double lengthTol = GetDouble(parameters, "鍩哄噯闀垮害鍏樊");
            double width = GetDouble(parameters, "鏍囧噯瀹藉害");
            double widthTol = GetDouble(parameters, "鏍囧噯瀹藉害鍏樊");
            double area = length * width;

            double hole = GetDouble(parameters, "瀛斿緞");
            double holeTol = GetDouble(parameters, "瀛斿緞鍏樊");
            double roundness = GetDouble(parameters, "鍦嗗害");
            double roundLower = GetDouble(parameters, "鍦嗗害涓嬮檺");
            double roundUpper = GetDouble(parameters, "鍦嗗害涓婇檺");

            double flatness = GetDouble(parameters, "骞虫暣搴?);
            double flatLower = GetDouble(parameters, "骞虫暣搴︿笅闄?);
            double flatUpper = GetDouble(parameters, "骞虫暣搴︿笂闄?);

            var measurements = new List<AlgorithmMeasurement>
            {
                BuildMeasurement("鍩哄噯闀垮害", length, lengthTol),
                BuildMeasurement("鏍囧噯瀹藉害", width, widthTol),
                BuildMeasurement("闈㈢Н", area, null),
                BuildMeasurement("瀛斿緞", hole, holeTol),
                BuildMeasurement("鍦嗗害", roundness, roundLower, roundUpper),
                BuildMeasurement("骞虫暣搴?, flatness, flatLower, flatUpper)
            };

            bool hasNg = measurements.Exists(m => m.IsOutOfRange);

            return new AlgorithmResult
            {
                EngineId = EngineId,
                EngineVersion = EngineVersion,
                Status = AlgorithmExecutionStatus.Success,
                IsOk = !hasNg,
                DefectType = hasNg ? "NG" : "鑹搧",
                Description = "绀轰緥椤圭洰绠楁硶杈撳嚭",
                Measurements = measurements
            };
        }

        private static AlgorithmMeasurement BuildMeasurement(string name, double value, double? tolerance)
        {
            double lower = double.MinValue;
            double upper = double.MaxValue;
            if (tolerance.HasValue)
            {
                lower = value - tolerance.Value;
                upper = value + tolerance.Value;
            }

            return BuildMeasurement(name, value, lower, upper);
        }

        private static AlgorithmMeasurement BuildMeasurement(string name, double value, double lower, double upper)
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
                IsOutOfRange = outOfRange
            };
        }

        private static double GetDouble(IDictionary<string, string> parameters, string key)
        {
            if (parameters == null || string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            if (parameters.TryGetValue(key, out var raw) && double.TryParse(raw, out var value))
            {
                return value;
            }

            return 0;
        }
    }
}
