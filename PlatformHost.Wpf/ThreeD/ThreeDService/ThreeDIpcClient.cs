using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Json;
using GlueInspect.ThreeD.Contracts;

namespace WpfApp2.ThreeD
{
    internal static class ThreeDIpcClient
    {
        public static ThreeDIpcResponse Send(string pipeName, ThreeDIpcRequest request, int timeoutMs)
        {
            using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None))
            {
                client.Connect(timeoutMs);

                var writer = new BinaryWriter(client);
                var reader = new BinaryReader(client);

                var reqBytes = Serialize(request);
                writer.Write(reqBytes.Length);
                writer.Write(reqBytes);
                writer.Flush();

                var respLen = reader.ReadInt32();
                var respBytes = reader.ReadBytes(respLen);
                return Deserialize<ThreeDIpcResponse>(respBytes);
            }
        }

        private static byte[] Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return ms.ToArray();
            }
        }

        private static T Deserialize<T>(byte[] bytes)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(bytes))
            {
                return (T)serializer.ReadObject(ms);
            }
        }
    }
}

