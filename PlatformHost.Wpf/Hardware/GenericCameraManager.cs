using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WpfApp2.Hardware
{
    public static class GenericCameraManager
    {
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "GenericCameraProfiles.json");
        private static Dictionary<string, GenericCameraProfile> _profiles;

        private static readonly string[] VendorList =
        {
            "Basler",
            "Hikvision",
            "Dahua",
            "MindVision",
            "Keyence",
            "Other"
        };

        public static IReadOnlyList<string> SupportedVendors => VendorList;

        public static GenericCameraProfile GetProfile(string cameraId, string displayName = null)
        {
            EnsureLoaded();

            if (string.IsNullOrWhiteSpace(cameraId))
            {
                cameraId = CameraRole.Flying.ToString();
            }

            if (!_profiles.TryGetValue(cameraId, out var profile) || profile == null)
            {
                profile = CreateDefaultProfile(cameraId, displayName);
                _profiles[cameraId] = profile;
                SaveInternal();
            }
            else if (!string.IsNullOrWhiteSpace(displayName) && !string.Equals(profile.DisplayName, displayName, StringComparison.Ordinal))
            {
                profile.DisplayName = displayName;
                SaveInternal();
            }

            return CloneProfile(profile);
        }

        public static GenericCameraProfile GetProfile(CameraRole role)
        {
            return GetProfile(role.ToString(), role.ToString());
        }

        public static void SaveProfile(GenericCameraProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            EnsureLoaded();

            var key = profile.CameraId;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = profile.Role.ToString();
                profile.CameraId = key;
            }

            _profiles[key] = profile;
            SaveInternal();
        }

        private static void EnsureLoaded()
        {
            if (_profiles != null)
            {
                return;
            }

            _profiles = LoadInternal();
        }

        private static Dictionary<string, GenericCameraProfile> LoadInternal()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var profiles = JsonConvert.DeserializeObject<List<GenericCameraProfile>>(json) ?? new List<GenericCameraProfile>();
                    var map = new Dictionary<string, GenericCameraProfile>(StringComparer.OrdinalIgnoreCase);
                    foreach (var profile in profiles)
                    {
                        if (profile == null)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(profile.CameraId))
                        {
                            profile.CameraId = profile.Role.ToString();
                        }

                        if (string.IsNullOrWhiteSpace(profile.DisplayName))
                        {
                            profile.DisplayName = profile.CameraId;
                        }

                        map[profile.CameraId] = profile;
                    }

                    return map;
                }
            }
            catch
            {
                // 忽略读取异常，转为默认配置
            }

            return new Dictionary<string, GenericCameraProfile>(StringComparer.OrdinalIgnoreCase)
            {
                { "Default", CreateDefaultProfile("Default", "默认相机") }
            };
        }

        private static void SaveInternal()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                var profiles = new List<GenericCameraProfile>(_profiles.Values);
                var json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
                File.WriteAllText(ConfigFile, json);
            }
            catch
            {
                // 写入失败时保持静默，避免影响主流程
            }
        }

        private static GenericCameraProfile CreateDefaultProfile(string cameraId, string displayName)
        {
            var role = cameraId == CameraRole.Fixed.ToString() ? CameraRole.Fixed : CameraRole.Flying;
            return new GenericCameraProfile
            {
                Role = role,
                CameraId = cameraId ?? CameraRole.Flying.ToString(),
                DisplayName = displayName ?? string.Empty,
                Vendor = "Generic",
                Model = role == CameraRole.Flying ? "FlyingCam" : "FixedCam",
                SerialNumber = string.Empty,
                Settings = new GenericCameraSettings()
            };
        }

        private static GenericCameraProfile CloneProfile(GenericCameraProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return new GenericCameraProfile
            {
                Role = profile.Role,
                CameraId = profile.CameraId,
                DisplayName = profile.DisplayName,
                Vendor = profile.Vendor,
                Model = profile.Model,
                SerialNumber = profile.SerialNumber,
                Settings = new GenericCameraSettings
                {
                    ExposureTimeUs = profile.Settings?.ExposureTimeUs ?? 0,
                    Gain = profile.Settings?.Gain ?? 0,
                    TriggerSource = profile.Settings?.TriggerSource ?? CameraTriggerSource.External,
                    TriggerEnabled = profile.Settings?.TriggerEnabled ?? false,
                    TriggerDelayUs = profile.Settings?.TriggerDelayUs ?? 0
                }
            };
        }
    }
}
