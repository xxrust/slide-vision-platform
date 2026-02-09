using System;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// 硬件控制器工厂 - 提供IO和PLC控制器实例
    /// </summary>
    public static class HardwareControllerFactory
    {
        /// <summary>
        /// 创建IO控制器实例
        /// </summary>
        /// <param name="useRealHardware">是否使用真实硬件（false则返回Null实现）</param>
        /// <returns>IO控制器实例</returns>
        public static IIoController CreateIoController(bool useRealHardware = true)
        {
            if (useRealHardware)
            {
                return new IoControllerAdapter();
            }
            else
            {
                return new NullIoController();
            }
        }

        /// <summary>
        /// 创建PLC控制器实例
        /// </summary>
        /// <param name="useRealHardware">是否使用真实硬件（false则返回Null实现）</param>
        /// <returns>PLC控制器实例</returns>
        public static IPlcController CreatePlcController(bool useRealHardware = true)
        {
            if (useRealHardware)
            {
                return new PlcControllerAdapter();
            }
            else
            {
                return new NullPlcController();
            }
        }
    }
}
