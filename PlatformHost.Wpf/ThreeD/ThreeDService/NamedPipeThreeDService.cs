using System;
using System.Diagnostics;
using System.IO;
using GlueInspect.ThreeD.Contracts;

namespace WpfApp2.ThreeD
{
    /// <summary>
    /// NamedPipe client for GlueInspect.ThreeD.Host.exe.
    /// This type intentionally stays free of any Keyence/LjDev/Ljd references.
    /// </summary>
    public sealed class NamedPipeThreeDService : IThreeDService
    {
        public const string DefaultPipeName = "GlueInspect.ThreeD";

        private readonly string _pipeName;
        private readonly string _hostExePath;
        private readonly string _codeMeterCheckExePath;

        public NamedPipeThreeDService(
            string pipeName = DefaultPipeName,
            string hostExePath = null,
            string codeMeterCheckExePath = null)
        {
            _pipeName = string.IsNullOrWhiteSpace(pipeName) ? DefaultPipeName : pipeName;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _hostExePath = hostExePath ?? Path.Combine(baseDir, "GlueInspect.ThreeD.Host.exe");
            _codeMeterCheckExePath = codeMeterCheckExePath ?? Path.Combine(baseDir, "CodeMeterCheck.exe");
        }

        public ThreeDStatus GetStatus(int timeoutMs = 3000)
        {
            var resp = TrySend("GetStatus", timeoutMs, out var errorMessage);
            if (resp?.Status != null) return resp.Status;

            return new ThreeDStatus
            {
                Status = ThreeDAvailabilityStatus.Error,
                Message = errorMessage ?? "3D status unavailable.",
                HostVersion = "unknown"
            };
        }

        public ThreeDExecuteResult ExecuteLocalImages(ThreeDExecuteLocalImagesRequest request, int timeoutMs = 30000)
        {
            var resp = TrySend(new ThreeDIpcRequest { Command = "ExecuteLocalImages", ExecuteLocalImages = request }, timeoutMs, out var errorMessage);
            return resp?.ExecuteResult ?? new ThreeDExecuteResult { Success = false, ErrorMessage = errorMessage ?? "3D execute failed." };
        }

        public bool SaveAfterJudgement(ThreeDSaveAfterJudgementRequest request, out string errorMessage, int timeoutMs = 30000)
        {
            var resp = TrySend(new ThreeDIpcRequest { Command = "SaveAfterJudgement", SaveAfterJudgement = request }, timeoutMs, out errorMessage);
            return resp != null && resp.Success;
        }

        private ThreeDIpcResponse TrySend(string command, int timeoutMs, out string errorMessage)
        {
            return TrySend(new ThreeDIpcRequest { Command = command }, timeoutMs, out errorMessage);
        }

        private ThreeDIpcResponse TrySend(ThreeDIpcRequest req, int timeoutMs, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                return ThreeDIpcClient.Send(_pipeName, req, timeoutMs);
            }
            catch (TimeoutException)
            {
                errorMessage = "3D Host connect timeout.";
                return null;
            }
            catch (IOException)
            {
                // Host not running or pipe not ready; attempt to start if authorized.
                if (!TryEnsureAuthorizedAndStartHost(out errorMessage))
                {
                    return null;
                }

                try
                {
                    return ThreeDIpcClient.Send(_pipeName, req, timeoutMs);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    return null;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        private bool TryEnsureAuthorizedAndStartHost(out string errorMessage)
        {
            errorMessage = null;

            if (!File.Exists(_hostExePath))
            {
                errorMessage = "3D Host not installed: " + _hostExePath;
                return false;
            }

            // CodeMeter check is optional: if the check exe exists, enforce it.
            if (File.Exists(_codeMeterCheckExePath))
            {
                var exitCode = RunProcess(_codeMeterCheckExePath, timeoutMs: 10000);
                if (exitCode != 0)
                {
                    errorMessage = "CodeMeter authorization failed (exitCode=" + exitCode + ").";
                    return false;
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _hostExePath,
                    Arguments = _pipeName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_hostExePath) ?? AppDomain.CurrentDomain.BaseDirectory
                });

                // Give the host a short time to create the pipe.
                System.Threading.Thread.Sleep(300);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to start 3D Host: " + ex.Message;
                return false;
            }
        }

        private static int RunProcess(string exePath, int timeoutMs)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null) return 98;
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    return 99;
                }
                return process.ExitCode;
            }
        }

        public void Dispose() { }
    }
}
