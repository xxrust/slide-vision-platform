using System;

namespace WpfApp2.UI.Models
{
    public sealed class CicdItemLimitInfo
    {
        public string ItemName { get; set; }
        public string LowerLimit { get; set; }
        public string UpperLimit { get; set; }

        public bool HasAnyLimit =>
            !string.IsNullOrWhiteSpace(LowerLimit) || !string.IsNullOrWhiteSpace(UpperLimit);
    }
}

