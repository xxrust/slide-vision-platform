using System;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// IOManager的适配器实现 - 将静态类适配为接口
    /// </summary>
    public sealed class IoControllerAdapter : IIoController
    {
        public bool IsInitialized => IOManager.IsInitialized;

        public bool Initialize()
        {
            return IOManager.Initialize();
        }

        public void SetDetectionResult(bool isOK)
        {
            IOManager.SetDetectionResult(isOK);
        }

        public void SetSingleOutput(int pinNumber, bool isHigh)
        {
            IOManager.SetSingleOutput(pinNumber, isHigh);
        }

        public bool[] GetAllOutputStates()
        {
            return IOManager.GetAllOutputStates();
        }

        public void ResetAllOutputs()
        {
            IOManager.ResetAllOutputs();
        }

        public bool SetActiveDevice(string deviceId)
        {
            return IOManager.SetActiveDevice(deviceId);
        }
    }
}
