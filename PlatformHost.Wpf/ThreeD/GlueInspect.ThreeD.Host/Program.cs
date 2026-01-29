using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using GlueInspect.ThreeD.Contracts;
using Keyence.LjDevCommon;
using Keyence.LjDevMeasure;
using Keyence.LjDev3dView;
using LjdSampleWrapper;

namespace GlueInspect.ThreeD.Host
{
    internal static class Program
    {
        // Keep the name stable; client proxy will use it.
        internal const string DefaultPipeName = "GlueInspect.ThreeD";
        private const string DefaultLaserIpPort = "192.168.0.1:24691:24692";

        private static readonly object Sync = new object();
        private static LjdMeasureEx _measureEx;
        private static ThreeDConfig _activeConfig;
        private static bool _lastIsJudgeAllOK;

        [STAThread]
        private static int Main(string[] args)
        {
            var pipeName = args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : DefaultPipeName;

            try
            {
                // Single-instance server loop: one client at a time (sufficient for current app flow).
                while (true)
                {
                    using (var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        server.WaitForConnection();
                        HandleClient(server);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText("GlueInspect.ThreeD.Host.crash.log", DateTime.Now + " " + ex + Environment.NewLine);
                }
                catch { }
                return 99;
            }
        }

        private static void HandleClient(Stream stream)
        {
            var reader = new BinaryReader(stream);
            var writer = new BinaryWriter(stream);

            while (true)
            {
                ThreeDIpcRequest req;
                try
                {
                    // Length-prefixed JSON message
                    var length = reader.ReadInt32();
                    var bytes = reader.ReadBytes(length);
                    req = ThreeDIpcJson.Deserialize<ThreeDIpcRequest>(bytes);
                }
                catch (EndOfStreamException)
                {
                    return;
                }
                catch (IOException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    WriteResponse(writer, new ThreeDIpcResponse
                    {
                        RequestId = Guid.Empty,
                        Success = false,
                        ErrorMessage = "Bad request: " + ex.Message
                    });
                    continue;
                }

                var resp = Dispatch(req);
                WriteResponse(writer, resp);
            }
        }

        private static void WriteResponse(BinaryWriter writer, ThreeDIpcResponse resp)
        {
            var bytes = ThreeDIpcJson.Serialize(resp);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            writer.Flush();
        }

        private static ThreeDIpcResponse Dispatch(ThreeDIpcRequest req)
        {
            var command = (req?.Command ?? string.Empty).Trim();
            var resp = new ThreeDIpcResponse
            {
                RequestId = req != null ? req.RequestId : Guid.Empty,
                Success = true
            };

            try
            {
                switch (command)
                {
                    case "Ping":
                        resp.Status = GetStatus();
                        return resp;
                    case "GetStatus":
                        resp.Status = GetStatus();
                        return resp;
                    case "ExecuteLocalImages":
                        resp.ExecuteResult = ExecuteLocalImages(req.ExecuteLocalImages);
                        return resp;
                    case "SaveAfterJudgement":
                        resp.Success = SaveAfterJudgement(req.SaveAfterJudgement, out string saveError);
                        resp.ErrorMessage = saveError;
                        return resp;
                    default:
                        resp.Success = false;
                        resp.ErrorMessage = "Unknown command: " + command;
                        return resp;
                }
            }
            catch (Exception ex)
            {
                resp.Success = false;
                resp.ErrorMessage = ex.Message;
                return resp;
            }
        }

        private static ThreeDStatus GetStatus()
        {
            try
            {
                // Force load/use of a Keyence type. If CodeMeter is missing, this can throw.
                var type = typeof(Lj3DView);
                _ = type.FullName;

                return new ThreeDStatus
                {
                    Status = ThreeDAvailabilityStatus.Available,
                    Message = "OK",
                    HostVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion
                };
            }
            catch (Exception ex)
            {
                return new ThreeDStatus
                {
                    Status = ThreeDAvailabilityStatus.NotAuthorized,
                    Message = ex.Message,
                    HostVersion = "unknown"
                };
            }
        }

        private static ThreeDExecuteResult ExecuteLocalImages(ThreeDExecuteLocalImagesRequest request)
        {
            if (request == null)
            {
                return new ThreeDExecuteResult { Success = false, ErrorMessage = "ExecuteLocalImages request is null." };
            }

            if (request.Config == null || !request.Config.Enable3DDetection)
            {
                return new ThreeDExecuteResult { Success = true, IsJudgeAllOK = true, ExecuteTimeMs = 0 };
            }

            if (string.IsNullOrWhiteSpace(request.HeightImagePath) || string.IsNullOrWhiteSpace(request.GrayImagePath))
            {
                return new ThreeDExecuteResult { Success = false, ErrorMessage = "Height/Gray image path is empty." };
            }

            if (!File.Exists(request.HeightImagePath) || !File.Exists(request.GrayImagePath))
            {
                return new ThreeDExecuteResult { Success = false, ErrorMessage = "Height/Gray image file not found." };
            }

            try
            {
                lock (Sync)
                {
                    EnsureMeasureEx(request.Config);

                    var heightImg = new LHeightImage();
                    var grayImg = new LGrayImage();
                    try
                    {
                        heightImg.Read(request.HeightImagePath);
                        grayImg.Read(request.GrayImagePath);

                        if (!heightImg.IsEnable() || !grayImg.IsEnable())
                        {
                            return new ThreeDExecuteResult { Success = false, ErrorMessage = "Failed to read 3D images (IsEnable=false)." };
                        }

                        var sw = Stopwatch.StartNew();
                        bool ok = _measureEx.Execute(new[] { heightImg }, new[] { grayImg }, exportData: false, saveImage: false);
                        sw.Stop();

                        _lastIsJudgeAllOK = _measureEx.IsJudgeAllOK;

                        var result = new ThreeDExecuteResult
                        {
                            Success = ok,
                            ErrorMessage = ok ? null : "Execute returned false.",
                            IsJudgeAllOK = _measureEx.IsJudgeAllOK,
                            ExecuteTimeMs = sw.Elapsed.TotalMilliseconds
                        };

                        // Minimal item list: keep overall judge so main process can incorporate 3D result.
                        result.Items.Add(new ThreeDDetectionItem
                        {
                            Name = "JudgeAll",
                            ValueString = _measureEx.IsJudgeAllOK ? "OK" : "NG",
                            IsOutOfRange = !_measureEx.IsJudgeAllOK,
                            ToolIndex = 0
                        });

                        // Best-effort: include display strings for later UI diagnostics without coupling main process to Keyence types.
                        if (_measureEx.GetDisplayText(out string[] resultText, out string[] judgeText))
                        {
                            if (judgeText != null && judgeText.Length > 0)
                            {
                                result.Extra["JudgeText"] = string.Join("\n", judgeText);
                            }

                            if (resultText != null && resultText.Length > 0)
                            {
                                result.Extra["ResultText"] = string.Join("\n", resultText);
                            }
                        }

                        return result;
                    }
                    finally
                    {
                        // LHeightImage/LGrayImage are unmanaged wrappers but do not implement IDisposable in this SDK version.
                    }
                }
            }
            catch (Exception ex)
            {
                return new ThreeDExecuteResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static void EnsureMeasureEx(ThreeDConfig config)
        {
            if (_measureEx != null && IsSameConfig(_activeConfig, config))
            {
                return;
            }

            try
            {
                if (_measureEx != null)
                {
                    _measureEx.Dispose();
                }
            }
            catch
            {
                // Best effort; host must stay up even if SDK dispose throws.
            }
            finally
            {
                _measureEx = null;
            }

            if (string.IsNullOrWhiteSpace(config.ProjectName) || string.IsNullOrWhiteSpace(config.ProjectFolder))
            {
                throw new InvalidOperationException("3D project name/folder is not configured.");
            }

            // Keep the same constructor args as the legacy in-proc flow; local image execution does not require StartImageReceiving.
            _measureEx = new LjdMeasureEx(
                config.ProjectName,
                config.ProjectFolder,
                DefaultLaserIpPort,
                false,
                0,
                config.ReCompile,
                true,
                "");

            _activeConfig = new ThreeDConfig
            {
                Enable3DDetection = config.Enable3DDetection,
                ProjectName = config.ProjectName,
                ProjectFolder = config.ProjectFolder,
                ReCompile = config.ReCompile
            };
        }

        private static bool IsSameConfig(ThreeDConfig a, ThreeDConfig b)
        {
            if (a == null || b == null) return false;
            return a.Enable3DDetection == b.Enable3DDetection
                && string.Equals(a.ProjectName ?? string.Empty, b.ProjectName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.ProjectFolder ?? string.Empty, b.ProjectFolder ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && a.ReCompile == b.ReCompile;
        }

        private static bool SaveAfterJudgement(ThreeDSaveAfterJudgementRequest request, out string errorMessage)
        {
            errorMessage = null;

            if (request == null)
            {
                errorMessage = "SaveAfterJudgement request is null.";
                return false;
            }

            lock (Sync)
            {
                if (_measureEx == null || _measureEx.ExecuteResult == null || !_measureEx.ExecuteResult.IsEnable)
                {
                    errorMessage = "No 3D execute result available in Host.";
                    return false;
                }

                // If main process calls us unconditionally in the future, keep the old behavior: OK + saveAll=false => skip saving.
                if (!request.SaveAllImages && _lastIsJudgeAllOK)
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(request.RootDirectory))
                {
                    errorMessage = "RootDirectory is empty.";
                    return false;
                }

                string targetFolder = Path.Combine(request.RootDirectory, "3D");
                Directory.CreateDirectory(targetFolder);

                string imageNumberStr = request.ImageNumber <= 0 ? "1" : request.ImageNumber.ToString();

                var executeResult = _measureEx.ExecuteResult;
                var rawHeightImages = executeResult.RawHeightImages;
                var rawGrayImages = executeResult.RawGrayImages;

                bool wroteAny = false;

                if (rawHeightImages != null)
                {
                    foreach (var h in rawHeightImages)
                    {
                        if (h.IsEnable())
                        {
                            string heightPath = Path.Combine(targetFolder, "height_" + imageNumberStr + ".png");
                            var res = h.Write(heightPath);
                            if (res != LFileIOErrorCode.Success)
                            {
                                errorMessage = "Save height failed: " + res;
                                return false;
                            }
                            wroteAny = true;
                            break;
                        }
                    }
                }

                if (rawGrayImages != null)
                {
                    foreach (var g in rawGrayImages)
                    {
                        if (g.IsEnable())
                        {
                            string grayPath = Path.Combine(targetFolder, "gray_" + imageNumberStr + ".png");
                            var res = g.Write(grayPath);
                            if (res != LFileIOErrorCode.Success)
                            {
                                errorMessage = "Save gray failed: " + res;
                                return false;
                            }
                            wroteAny = true;
                            break;
                        }
                    }
                }

                if (!wroteAny)
                {
                    errorMessage = "No enabled raw images in execute result.";
                    return false;
                }

                return true;
            }
        }
    }
}
