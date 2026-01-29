using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WpfApp2.Algorithms
{
    /// <summary>
    /// 算法全局变量存储（与具体算法引擎解耦）
    /// </summary>
    public static class AlgorithmGlobalVariables
    {
        private static readonly ConcurrentDictionary<string, string> Values =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Set(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            Values[name] = value ?? string.Empty;
        }

        public static string Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return Values.TryGetValue(name, out var value) ? value : string.Empty;
        }

        public static IReadOnlyDictionary<string, string> Snapshot()
        {
            return new Dictionary<string, string>(Values, StringComparer.OrdinalIgnoreCase);
        }

        public static void AppendTo(IDictionary<string, string> target, bool overwrite = false)
        {
            if (target == null)
            {
                return;
            }

            foreach (var pair in Values)
            {
                if (overwrite || !target.ContainsKey(pair.Key))
                {
                    target[pair.Key] = pair.Value;
                }
            }
        }
    }
}
