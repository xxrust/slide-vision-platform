namespace WpfApp2.Hardware
{
    public enum CameraRole
    {
        Flying,
        Fixed
    }

    public enum CameraTriggerSource
    {
        Software,
        External,
        Line1,
        Line2
    }

    public sealed class GenericCameraSettings
    {
        public double ExposureTimeUs { get; set; } = 1000;
        public double Gain { get; set; } = 1.0;
        public CameraTriggerSource TriggerSource { get; set; } = CameraTriggerSource.External;
        public bool TriggerEnabled { get; set; } = true;
        public double TriggerDelayUs { get; set; } = 0;
    }

    public sealed class GenericCameraProfile
    {
        public CameraRole Role { get; set; } = CameraRole.Flying;
        public string Vendor { get; set; } = "Generic";
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public GenericCameraSettings Settings { get; set; } = new GenericCameraSettings();
    }
}
