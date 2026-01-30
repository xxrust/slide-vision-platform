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
        private static Dictionary<CameraRole, GenericCameraProfile> _profiles;

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

        public static GenericCameraProfile GetProfile(CameraRole role)
        {
            EnsureLoaded();

            if (!_profiles.TryGetValue(role, out var profile) || profile == null)
            {
                profile = CreateDefaultProfile(role);
                _profiles[role] = profile;
                SaveInternal();
            }

            return CloneProfile(profile);
        }

        public static void SaveProfile(GenericCameraProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            EnsureLoaded();
            _profiles[profile.Role] = profile;
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

        private static Dictionary<CameraRole, GenericCameraProfile> LoadInternal()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var profiles = JsonConvert.DeserializeObject<List<GenericCameraProfile>>(json) ?? new List<GenericCameraProfile>();
                    var map = new Dictionary<CameraRole, GenericCameraProfile>();
                    foreach (var profile in profiles)
                    {
                        if (profile == null)
                        {
                            continue;
                        }

                        map[profile.Role] = profile;
                    }

                    return map;
                }
            }
            catch
            {
                // 忽略读取异常，转为默认配置
            }

            return new Dictionary<CameraRole, GenericCameraProfile>
            {
                { CameraRole.Flying, CreateDefaultProfile(CameraRole.Flying) },
                { CameraRole.Fixed, CreateDefaultProfile(CameraRole.Fixed) }
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

        private static GenericCameraProfile CreateDefaultProfile(CameraRole role)
        {
            return new GenericCameraProfile
            {
                Role = role,
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
