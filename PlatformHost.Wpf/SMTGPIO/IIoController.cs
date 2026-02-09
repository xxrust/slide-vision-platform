using System;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// IO控制器接口抽象
    /// </summary>
    public interface IIoController
    {
        /// <summary>
        /// IO控制器是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 初始化IO控制器
        /// </summary>
        /// <returns>是否初始化成功</returns>
        bool Initialize();

        /// <summary>
        /// 设置检测结果输出到IO端口
        /// </summary>
        /// <param name="isOK">检测结果，true为OK，false为NG</param>
        void SetDetectionResult(bool isOK);

        /// <summary>
        /// 设置单个输出口的状态
        /// </summary>
        /// <param name="pinNumber">引脚号 (1-4)</param>
        /// <param name="isHigh">是否为高电平</param>
        void SetSingleOutput(int pinNumber, bool isHigh);

        /// <summary>
        /// 获取所有输出口的状态
        /// </summary>
        /// <returns>输出状态数组，索引0-3对应IO1-IO4</returns>
        bool[] GetAllOutputStates();

        /// <summary>
        /// 复位所有IO输出
        /// </summary>
        void ResetAllOutputs();

        /// <summary>
        /// 设置活动设备
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>是否设置成功</returns>
        bool SetActiveDevice(string deviceId);
    }
}
