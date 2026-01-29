using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace VM.PlatformSDKCS
{
    public static class ImvsSdkDefine
    {
        public struct IMVS_MODULE_WORK_STAUS
        {
            public int nProcessID;
            public int nWorkStatus;
        }
    }
}

namespace VM.Core
{
    public sealed class VmException : Exception
    {
        public int errorCode { get; } = 0;

        public VmException() { }
        public VmException(string message) : base(message) { }
        public VmException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class VmSolution : IDisposable
    {
        private static readonly VmSolution InstanceValue = new VmSolution();

        public static VmSolution Instance => InstanceValue;

        public static event Action<VM.PlatformSDKCS.ImvsSdkDefine.IMVS_MODULE_WORK_STAUS> OnWorkStatusEvent;

        public static void Load(string path)
        {
            // No-op in OpenCV-only mode.
        }

        public static void Save()
        {
            // No-op in OpenCV-only mode.
        }

        public object this[string name] => null;

        public void CloseSolution()
        {
            // No-op in OpenCV-only mode.
        }

        public void Dispose()
        {
            // No-op in OpenCV-only mode.
        }
    }

    public sealed class VmProcedure
    {
        public ModuResult ModuResult { get; } = new ModuResult();

        public void Run()
        {
            // No-op in OpenCV-only mode.
        }
    }

    public sealed class ModuResult
    {
        public OutputString GetOutputString(string name)
        {
            return OutputString.Empty;
        }
    }

    public sealed class OutputString
    {
        public static OutputString Empty { get; } = new OutputString();

        public OutputStringValue[] astStringVal { get; } = new[] { new OutputStringValue() };
    }

    public sealed class OutputStringValue
    {
        public string strValue { get; set; } = string.Empty;
    }
}

namespace IMVSHPFeatureMatchModuCs
{
    public class IMVSHPFeatureMatchModuTool
    {
        public void ImportModelData(string[] paths)
        {
            // No-op in OpenCV-only mode.
        }
    }
}

namespace IMVSImageEnhanceModuCs
{
    public class IMVSImageEnhanceModuTool { }
}

namespace IMVSBlobFindModuCs
{
    public class IMVSBlobFindModuTool { }
}

namespace IMVSLineFindModuCs
{
    public class IMVSLineFindModuTool { }
}

namespace IMVSCircleFindModuCs
{
    public class IMVSCircleFindModuTool { }
}

namespace IMVSCnnFlawModuCCs
{
    public class IMVSCnnFlawModuCTool { }
}

namespace ImageSourceModuleCs
{
    public class ImageSourceModuleTool
    {
        public void SetImagePath(string path)
        {
            // No-op in OpenCV-only mode.
        }
    }
}

namespace SaveImageCs
{
    public class SaveImageTool { }
}

namespace GlobalVariableModuleCs
{
    public class GlobalVariableModuleTool
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void SetGlobalVar(string name, string value)
        {
            _values[name] = value ?? string.Empty;
        }

        public string GetGlobalVar(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : string.Empty;
        }
    }
}

namespace GlobalCameraModuleCs
{
    public class GlobalCameraModuleTool
    {
        public void SaveParamToUser1()
        {
            // No-op in OpenCV-only mode.
        }
    }
}

namespace DataQueueModuleCs
{
    public class DataQueueModuleTool { }
}

namespace IWshRuntimeLibrary
{
    public interface IWshShortcut
    {
        string TargetPath { get; set; }
        string WorkingDirectory { get; set; }
        string Description { get; set; }
        string IconLocation { get; set; }
        void Save();
    }

    public class WshShell
    {
        public IWshShortcut CreateShortcut(string path)
        {
            return new WshShortcut(path);
        }
    }

    internal sealed class WshShortcut : IWshShortcut
    {
        private readonly string _path;

        public WshShortcut(string path)
        {
            _path = path;
        }

        public string TargetPath { get; set; }
        public string WorkingDirectory { get; set; }
        public string Description { get; set; }
        public string IconLocation { get; set; }

        public void Save()
        {
            // No-op in OpenCV-only mode.
        }
    }
}

namespace VMControls.WPF.Release
{
    public class VmRenderControl : UserControl
    {
        public object ModuleSource { get; set; }

        public void SaveOriginalImage(string path)
        {
            // No-op in OpenCV-only mode.
        }
    }

    public class VmParamsConfigControl : UserControl
    {
        public object ModuleSource { get; set; }
    }

    public class VmParamsConfigWithRenderControl : UserControl
    {
        public object ModuleSource { get; set; }
    }

    public class VmGlobalToolControl : UserControl
    {
        public object ModuleSource { get; set; }
    }

    public class VmMainViewConfigControl : UserControl
    {
        public object ModuleSource { get; set; }
    }

    public class VmProcedureConfigControl : UserControl
    {
        public object ModuleSource { get; set; }
    }

    public class VMWpfUserControl : UserControl
    {
    }
}
