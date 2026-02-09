using System;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// Null IO控制器实现 - 用于IO硬件不可用时
    /// </summary>
    public sealed class NullIoController : IIoController
    {
        public bool IsInitialized => false;

        public bool Initialize()
        {
            return false;
        }

        public void SetDetectionResult(bool isOK)
        {
            // No-op
        }

        public void SetSingleOutput(int pinNumber, bool isHigh)
        {
            // No-op
        }

        public bool[] GetAllOutputStates()
        {
            return new bool[4];
        }

        public void ResetAllOutputs()
        {
            // No-op
        }

        public bool SetActiveDevice(string deviceId)
        {
            return false;
        }
    }
}
