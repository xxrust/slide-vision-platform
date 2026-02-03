using System;
using System.Collections.Generic;
using System.Net;
using SciCamera.Net;

namespace WpfApp2.Hardware
{
    public sealed class OptCameraService
    {
        private static readonly IReadOnlyList<string> DefaultPixelFormatsInternal = new[]
        {
            "Mono8",
            "Mono10",
            "Mono12",
            "Mono16",
            "RGB8",
            "BayerRG8",
            "BayerBG8",
            "BayerGB8",
            "BayerGR8"
        };

        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(2);
        private DateTime _lastDiscovery = DateTime.MinValue;
        private List<OptCameraDeviceInfo> _cachedDevices = new List<OptCameraDeviceInfo>();
        private SciCam.SCI_DEVICE_INFO_LIST _cachedDeviceList = new SciCam.SCI_DEVICE_INFO_LIST();

        public static OptCameraService Instance { get; } = new OptCameraService();

        public static IReadOnlyList<string> DefaultPixelFormats => DefaultPixelFormatsInternal;

        private OptCameraService()
        {
        }

        public IReadOnlyList<OptCameraDeviceInfo> DiscoverDevices(bool forceRefresh, out string message)
        {
            if (!forceRefresh && _cachedDevices.Count > 0 && DateTime.UtcNow - _lastDiscovery < _cacheDuration)
            {
                message = $"复用缓存设备 {_cachedDevices.Count} 台";
                return _cachedDevices;
            }

            var deviceList = new SciCam.SCI_DEVICE_INFO_LIST();
            uint result = SciCam.DiscoveryDevices(ref deviceList,
                (uint)(SciCam.SciCamTLType.SciCam_TLType_Gige | SciCam.SciCamTLType.SciCam_TLType_Usb3));
            if (result != SciCam.SCI_CAMERA_OK)
            {
                message = $"搜索OPT相机失败: {result}";
                _cachedDevices = new List<OptCameraDeviceInfo>();
                return _cachedDevices;
            }

            var devices = new List<OptCameraDeviceInfo>();
            for (int i = 0; i < deviceList.count; i++)
            {
                var device = deviceList.pDevInfo[i];
                if (device.tlType == SciCam.SciCamTLType.SciCam_TLType_Gige)
                {
                    var gigeInfo = (SciCam.SCI_DEVICE_GIGE_INFO)SciCam.ByteToStruct(device.info.gigeInfo, typeof(SciCam.SCI_DEVICE_GIGE_INFO));
                    devices.Add(new OptCameraDeviceInfo
                    {
                        Index = i,
                        ConnectionType = "GigE",
                        Model = gigeInfo.modelName,
                        SerialNumber = gigeInfo.serialNumber,
                        IpAddress = new IPAddress(gigeInfo.ip).ToString()
                    });
                }
                else if (device.tlType == SciCam.SciCamTLType.SciCam_TLType_Usb3)
                {
                    var usbInfo = (SciCam.SCI_DEVICE_USB3_INFO)SciCam.ByteToStruct(device.info.usb3Info, typeof(SciCam.SCI_DEVICE_USB3_INFO));
                    devices.Add(new OptCameraDeviceInfo
                    {
                        Index = i,
                        ConnectionType = "U3V",
                        Model = usbInfo.modelName,
                        SerialNumber = usbInfo.serialNumber
                    });
                }
            }

            _cachedDevices = devices;
            _cachedDeviceList = deviceList;
            _lastDiscovery = DateTime.UtcNow;
            message = devices.Count == 0 ? "未发现OPT相机" : $"发现OPT相机 {devices.Count} 台";
            return _cachedDevices;
        }

        public bool TryGetPixelFormats(OptCameraDeviceInfo deviceInfo, out List<string> formats, out string current, out string message)
        {
            formats = new List<string>();
            current = string.Empty;
            if (deviceInfo == null)
            {
                message = "未选择相机";
                return false;
            }

            if (!TryResolveDeviceInfo(deviceInfo.SerialNumber, out var device, out message))
            {
                return false;
            }

            if (!TryOpenDevice(device, out var camera, out message))
            {
                return false;
            }

            try
            {
                var enumVal = new SciCam.SCI_NODE_VAL_ENUM();
                uint result = camera.GetEnumValue("PixelFormat", ref enumVal);
                if (result != SciCam.SCI_CAMERA_OK)
                {
                    message = $"读取像素格式失败: {result}";
                    return false;
                }

                for (int i = 0; i < enumVal.itemCount; i++)
                {
                    var item = enumVal.items[i];
                    formats.Add(item.desc);
                    if (enumVal.nVal == item.val)
                    {
                        current = item.desc;
                    }
                }

                message = formats.Count == 0 ? "相机未返回像素格式" : "像素格式读取成功";
                return formats.Count > 0;
            }
            finally
            {
                CloseDevice(camera);
            }
        }

        public bool TryApplyProfile(GenericCameraProfile profile, out string message)
        {
            if (profile == null)
            {
                message = "配置为空";
                return false;
            }

            if (!IsOptVendor(profile.Vendor))
            {
                message = "非OPT相机，跳过下发";
                return false;
            }

            var serial = profile.SerialNumber?.Trim();
            if (string.IsNullOrWhiteSpace(serial))
            {
                message = "未绑定序列号";
                return false;
            }

            if (!TryResolveDeviceInfo(serial, out var device, out message))
            {
                return false;
            }

            return TryApplySettings(device, profile.Settings, out message);
        }

        public static bool IsOptVendor(string vendor)
        {
            return !string.IsNullOrWhiteSpace(vendor) && vendor.Trim().Equals("OPT", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolveDeviceInfo(string serialNumber, out SciCam.SCI_DEVICE_INFO deviceInfo, out string message)
        {
            deviceInfo = default;
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                message = "序列号为空";
                return false;
            }

            DiscoverDevices(true, out _);
            if (_cachedDeviceList.count <= 0)
            {
                message = "未发现相机";
                return false;
            }

            for (int i = 0; i < _cachedDeviceList.count; i++)
            {
                var device = _cachedDeviceList.pDevInfo[i];
                string candidateSerial = string.Empty;
                if (device.tlType == SciCam.SciCamTLType.SciCam_TLType_Gige)
                {
                    var gigeInfo = (SciCam.SCI_DEVICE_GIGE_INFO)SciCam.ByteToStruct(device.info.gigeInfo, typeof(SciCam.SCI_DEVICE_GIGE_INFO));
                    candidateSerial = gigeInfo.serialNumber;
                }
                else if (device.tlType == SciCam.SciCamTLType.SciCam_TLType_Usb3)
                {
                    var usbInfo = (SciCam.SCI_DEVICE_USB3_INFO)SciCam.ByteToStruct(device.info.usb3Info, typeof(SciCam.SCI_DEVICE_USB3_INFO));
                    candidateSerial = usbInfo.serialNumber;
                }

                if (!string.IsNullOrWhiteSpace(candidateSerial) &&
                    string.Equals(candidateSerial.Trim(), serialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    deviceInfo = device;
                    message = "已匹配序列号";
                    return true;
                }
            }

            message = $"未找到序列号为 {serialNumber} 的相机";
            return false;
        }

        private bool TryOpenDevice(SciCam.SCI_DEVICE_INFO deviceInfo, out SciCam camera, out string message)
        {
            camera = new SciCam();
            uint result = camera.CreateDevice(ref deviceInfo);
            if (result != SciCam.SCI_CAMERA_OK)
            {
                message = $"创建相机失败: {result}";
                return false;
            }

            result = camera.OpenDevice();
            if (result != SciCam.SCI_CAMERA_OK)
            {
                message = $"打开相机失败: {result}";
                CloseDevice(camera);
                return false;
            }

            message = "相机已连接";
            return true;
        }

        private static void CloseDevice(SciCam camera)
        {
            if (camera == null)
            {
                return;
            }

            try
            {
                camera.CloseDevice();
            }
            catch
            {
                // 忽略关闭异常
            }

            try
            {
                camera.DeleteDevice();
            }
            catch
            {
                // 忽略删除异常
            }
        }

        private bool TryApplySettings(SciCam.SCI_DEVICE_INFO deviceInfo, GenericCameraSettings settings, out string message)
        {
            if (!TryOpenDevice(deviceInfo, out var camera, out message))
            {
                return false;
            }

            try
            {
                var errorList = new List<string>();

                if (!string.IsNullOrWhiteSpace(settings.PixelFormat))
                {
                    uint result = camera.SetEnumValueByString("PixelFormat", settings.PixelFormat);
                    if (result != SciCam.SCI_CAMERA_OK)
                    {
                        errorList.Add($"像素格式设置失败({settings.PixelFormat}): {result}");
                    }
                }

                if (!TrySetNumber(camera, new[] { "ExposureTime", "ExposureTimeAbs", "ExposureTimeRaw" }, settings.ExposureTimeUs))
                {
                    errorList.Add("曝光设置失败");
                }

                if (!TrySetNumber(camera, new[] { "Gain", "GainRaw" }, settings.Gain))
                {
                    errorList.Add("增益设置失败");
                }

                if (settings.FrameRate > 0)
                {
                    uint result = camera.SetBoolValue("AcquisitionFrameRateEnable", true);
                    if (result != SciCam.SCI_CAMERA_OK)
                    {
                        errorList.Add($"帧率使能失败: {result}");
                    }
                    else if (!TrySetNumber(camera,
                             new[] { "AcquisitionFrameRate", "AcquisitionFrameRateAbs", "ResultingFrameRate", "ResultingFrameRateAbs" },
                             settings.FrameRate))
                    {
                        errorList.Add("帧率设置失败");
                    }
                }

                string triggerMode = settings.TriggerEnabled ? "On" : "Off";
                uint modeResult = camera.SetEnumValueByString("TriggerMode", triggerMode);
                if (modeResult != SciCam.SCI_CAMERA_OK)
                {
                    errorList.Add($"触发模式设置失败({triggerMode}): {modeResult}");
                }

                if (settings.TriggerEnabled)
                {
                    string triggerSource = MapTriggerSource(settings.TriggerSource);
                    uint sourceResult = camera.SetEnumValueByString("TriggerSource", triggerSource);
                    if (sourceResult != SciCam.SCI_CAMERA_OK)
                    {
                        errorList.Add($"触发源设置失败({triggerSource}): {sourceResult}");
                    }
                }

                if (settings.TriggerDelayUs > 0)
                {
                    if (!TrySetNumber(camera, new[] { "TriggerDelay", "TriggerDelayAbs" }, settings.TriggerDelayUs))
                    {
                        errorList.Add("触发延时设置失败");
                    }
                }

                if (errorList.Count > 0)
                {
                    message = string.Join("；", errorList);
                    return false;
                }

                message = "参数已下发";
                return true;
            }
            finally
            {
                CloseDevice(camera);
            }
        }

        private static bool TrySetNumber(SciCam camera, IReadOnlyList<string> nodeNames, double value)
        {
            if (camera == null || nodeNames == null || nodeNames.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < nodeNames.Count; i++)
            {
                string nodeName = nodeNames[i];
                if (string.IsNullOrWhiteSpace(nodeName))
                {
                    continue;
                }

                int intValue = (int)Math.Round(value);
                uint result = camera.SetIntValue(nodeName, intValue);
                if (result == SciCam.SCI_CAMERA_OK)
                {
                    return true;
                }

                result = camera.SetFloatValue(nodeName, value);
                if (result == SciCam.SCI_CAMERA_OK)
                {
                    return true;
                }
            }

            return false;
        }

        private static string MapTriggerSource(CameraTriggerSource source)
        {
            switch (source)
            {
                case CameraTriggerSource.Software:
                    return "Software";
                case CameraTriggerSource.Line1:
                    return "Line1";
                case CameraTriggerSource.Line2:
                    return "Line2";
                default:
                    return "Line0";
            }
        }
    }
}
