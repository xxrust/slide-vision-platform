using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Slide.ThreeD.Host
{
    internal static class ThreeDIpcJson
    {
        public static byte[] Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return ms.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(bytes))
            {
                return (T)serializer.ReadObject(ms);
            }
        }

        public static string ToUtf8String(byte[] bytes) => Encoding.UTF8.GetString(bytes ?? new byte[0]);
    }
}

