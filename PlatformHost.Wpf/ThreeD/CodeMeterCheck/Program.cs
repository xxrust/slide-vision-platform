using System;
using Keyence.LjDev3dView;

namespace CodeMeterCheck
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                // Must actually touch a Keyence type to trigger CodeMeter validation.
                var type = typeof(Lj3DView);
                _ = type.FullName;
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}

