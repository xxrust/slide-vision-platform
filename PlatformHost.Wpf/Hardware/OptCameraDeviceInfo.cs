namespace WpfApp2.Hardware
{
    public sealed class OptCameraDeviceInfo
    {
        public int Index { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string ConnectionType { get; set; }
        public string IpAddress { get; set; }

        public string DisplayName
        {
            get
            {
                var model = string.IsNullOrWhiteSpace(Model) ? "OPT Camera" : Model.Trim();
                var serial = string.IsNullOrWhiteSpace(SerialNumber) ? "Unknown SN" : SerialNumber.Trim();
                var type = string.IsNullOrWhiteSpace(ConnectionType) ? "Device" : ConnectionType.Trim();
                if (!string.IsNullOrWhiteSpace(IpAddress))
                {
                    return $"{type}: {model} ({serial}) - {IpAddress}";
                }

                return $"{type}: {model} ({serial})";
            }
        }
    }
}
