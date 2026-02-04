using System;
using System.Windows.Media;

namespace WpfApp2.UI.Controls
{
    public sealed class TrayDefectVisual
    {
        public TrayDefectVisual(string iconFileName, Color fallbackColor)
        {
            if (string.IsNullOrWhiteSpace(iconFileName))
            {
                throw new ArgumentException("Icon file name is required.", nameof(iconFileName));
            }

            IconFileName = iconFileName;
            FallbackColor = fallbackColor;
        }

        public string IconFileName { get; }
        public Color FallbackColor { get; }
    }
}
