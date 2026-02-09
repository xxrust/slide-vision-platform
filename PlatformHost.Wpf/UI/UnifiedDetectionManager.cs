using System;
using System.Threading.Tasks;
using System.Windows;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 统一检测管理器 - 负责2D/3D检测编排和统一判定逻辑
    /// </summary>
    public class UnifiedDetectionManager
    {
        private bool _2DCompleted = false;
        private bool _3DCompleted = false;
        private bool _3DEnabled = false;
        // 🔧 移除锁：private readonly object _lock = new object();
        private Page1 _page1Instance;

        // 🔧 新增：检测模式和系统状态管理
        private DetectionMode _currentDetectionMode = DetectionMode.Full;
        private SystemDetectionState _systemState = SystemDetectionState.Idle;
        private bool _isSystemInitialized = false;
        private volatile bool _pendingExitTemplateConfigAfterUnifiedJudgement = false;

        // 2D超时检测定时器
        private System.Threading.Timer _2DTimeoutTimer = null;

        public bool Is2DCompleted => _2DCompleted;
        public bool Is3DCompleted => _3DCompleted;
        public bool Is3DEnabled => _3DEnabled;

        // 🔧 新增：检测模式和系统状态管理属性
        public DetectionMode CurrentDetectionMode => _currentDetectionMode;
        public SystemDetectionState SystemState => _systemState;
        public bool IsSystemInitialized => _isSystemInitialized;

        public void RequestExitTemplateConfigAfterNextUnifiedJudgement()
        {
            _pendingExitTemplateConfigAfterUnifiedJudgement = true;
        }

        /// <summary>
        /// 构造函数：需要Page1实例来执行统一判定
        /// </summary>
        public UnifiedDetectionManager(Page1 page1Instance)
        {
            _page1Instance = page1Instance;
        }

        /// <summary>
        /// 开始新的检测周期
        /// </summary>
        public void StartDetectionCycle(bool enable3D)
        {
            // 🔧 移除锁：直接操作
            _2DCompleted = false;
            _3DCompleted = false;
            _3DEnabled = enable3D && !ThreeDSettings.Is3DShielded;

            // 🔧 新增：停止之前的超时定时器
            Stop2DTimeoutTimer();

            // 🔧 修复重复读取：每次新检测周期开始时重置2D结果缓存
            Page1.ResetCached2DDetectionResult();
            Page1.ResetCached3DDetectionResult();

            LogManager.Info($"[检测管理器] 开始新的检测周期 - 3D启用: {_3DEnabled} (raw={enable3D}, shield={ThreeDSettings.Is3DShielded})");
        }

        /// <summary>
        /// 标记2D检测完成（只负责状态标记，由管理器统一控制数据更新与IO）
        /// </summary>
        public void Mark2DCompleted()
        {
            // 🔧 移除锁：工业控制中检测流程是顺序的，不需要锁保护
            // 正确处理重复调用
            if (_2DCompleted)
            {
                LogManager.Warning("[检测管理器] 2D检测已完成，忽略重复调用");
                return;
            }

            LogManager.Info("[检测管理器] 2D检测已完成");
            _2DCompleted = true;

            // 🔧 新增：2D完成时停止超时定时器
            Stop2DTimeoutTimer();

            // 统一检查并执行判定
            CheckAndExecuteUnifiedJudgement();
        }

        /// <summary>
        /// 标记3D检测完成（只负责状态标记，由管理器统一控制数据更新与IO）
        /// </summary>
        public void Mark3DCompleted()
        {
            // 🔧 移除锁：工业控制中检测流程是顺序的，不需要锁保护
            // 正确处理重复调用
            if (_3DCompleted)
            {
                LogManager.Warning("[检测管理器] 3D检测已完成，忽略重复调用");
                return;
            }

            LogManager.Info("[检测管理器] 3D检测已完成");
            _3DCompleted = true;

            // 🔧 新增：启动2秒超时检测定时器
            // 如果2D在2秒内未完成，则触发2D超时处理
            Start2DTimeoutTimer();

            // 统一检查并执行判定
            CheckAndExecuteUnifiedJudgement();
        }

        /// <summary>
        /// 检查检测周期是否完成
        /// </summary>
        public bool IsDetectionCycleComplete()
        {
            // 🔧 修复：配置模式下也需要根据3D使能状态等待检测完成
            // 当3D检测启用时，配置模式也需要等待3D检测完成
            if (_systemState == SystemDetectionState.TemplateConfiguring)
            {
                // 模板配置模式：如果启用了3D检测，也需要等待3D完成
                if (_3DEnabled)
                {
                    return _2DCompleted && _3DCompleted;
                }
                else
                {
                    // 未启用3D检测时，只需要2D完成即可
                    return _2DCompleted;
                }
            }

            // 🔧 使用内部状态，现在已通过CheckBox事件实现状态同步
            if (_3DEnabled)
            {
                // 3D启用时，需要2D和3D都完成
                return _2DCompleted && _3DCompleted;
            }
            else
            {
                // 3D未启用时，只需要2D完成
                return _2DCompleted;
            }
        }

        /// <summary>
        /// 获取检测状态描述
        /// </summary>
        public string GetStatusDescription()
        {
            // 🔧 移除锁：简单的状态描述不需要锁保护
            if (_3DEnabled)
            {
                return $"2D: {(_2DCompleted ? "✓" : "○")}, 3D: {(_3DCompleted ? "✓" : "○")}";
            }
            else
            {
                return $"2D: {(_2DCompleted ? "✓" : "○")} (仅2D模式)";
            }
        }

        /// <summary>
        /// 统一检查并执行判定（确保ExecuteUnifiedJudgementAndIO只被调用一次）
        /// </summary>
        private void CheckAndExecuteUnifiedJudgement()
        {
            // 🔧 移除锁：现在使用无锁设计，简化检测流程
            if (IsDetectionCycleComplete())
            {
                LogManager.Info($"[检测管理器] 检测周期完成 - 2D: {_2DCompleted}, 3D: {_3DCompleted} (启用: {_3DEnabled})");

                // 🔧 新增：系统测试模式特殊处理
                if (_currentDetectionMode == DetectionMode.SystemTest || _systemState == SystemDetectionState.SystemTesting)
                {
                    LogManager.Info("[检测管理器] 系统测试模式，执行特殊处理流程");

                    // 系统测试模式下，立即同步执行统一判定，确保性能测量准确
                    Task.Run(() =>
                    {
                        try
                        {
                            if (_page1Instance != null)
                            {
                                _page1Instance.ExecuteUnifiedJudgementAndIO();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"[检测管理器] 系统测试模式执行统一判定失败: {ex.Message}");
                        }
                    });
                }
                else
                {
                    LogManager.Info("[检测管理器] 执行标准统一判定和IO操作");

                    // 🔧 修复：异步调用统一判定，避免阻塞检测管理器
                    // 使用Task.Run确保异步执行不会阻塞当前线程
                    Task.Run(async () =>
                    {
                        try
                        {
                            // 只有管理器可以调用统一判定，确保只调用一次
                            if (_page1Instance != null)
                            {
                                await _page1Instance.ExecuteUnifiedJudgementAndIOAsync();
                                TryExitTemplateConfigAfterUnifiedJudgementIfRequested();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"[检测管理器] 异步执行统一判定失败: {ex.Message}");
                        }
                    });
                }

                // 先处理连续检测，再重置状态
                CheckAndHandleContinuousDetection();
                ResetInternal();
            }
            else
            {
                LogManager.Info($"[检测管理器] 检测周期未完成，等待其他检测 - 2D: {_2DCompleted}, 3D: {_3DCompleted} (启用: {_3DEnabled})");
            }
        }

        private void TryExitTemplateConfigAfterUnifiedJudgementIfRequested()
        {
            if (!_pendingExitTemplateConfigAfterUnifiedJudgement)
            {
                return;
            }

            _pendingExitTemplateConfigAfterUnifiedJudgement = false;

            if (_systemState != SystemDetectionState.TemplateConfiguring)
            {
                return;
            }

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                try
                {
                    var mainWindow = System.Windows.Application.Current?.MainWindow as MainWindow;
                    if (mainWindow?.ContentC?.Content is System.Windows.Controls.Frame activeFrame)
                    {
                        if (activeFrame.Content is TemplateConfigPage)
                        {
                            return;
                        }
                    }

                    SetSystemState(SystemDetectionState.WaitingForTrigger);
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"[检测管理器] 自动退出模板配置模式失败: {ex.Message}");
                }
            }));
        }

        /// <summary>
        /// 内部重置方法（假设已持有锁）
        /// </summary>
        private void ResetInternal()
        {
            _2DCompleted = false;
            _3DCompleted = false;
            // 不重置3D启用状态，这是配置状态，应该保持
            LogManager.Info("[检测管理器] 检测周期状态已重置，准备下次检测周期");
        }

        /// <summary>
        /// 检查并处理连续检测逻辑
        /// </summary>
        private void CheckAndHandleContinuousDetection()
        {
            try
            {
                if (_page1Instance == null || !_page1Instance.IsInImageTestMode())
                {
                    return;
                }

                // 检查连续检测模式
                var autoMode = _page1Instance._imageTestManager.AutoDetectionMode;
                bool isContinuousMode = autoMode != AutoDetectionMode.None;

                if (isContinuousMode)
                {
                    try
                    {
                        LogManager.Info("[检测管理器] 启动连续检测下一轮");

                        // 同步调用连续检测
                        if (Application.Current.Dispatcher.CheckAccess())
                        {
                            _page1Instance.HandleAutoDetectionAfterCompletion();
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _page1Instance.HandleAutoDetectionAfterCompletion();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"[检测管理器] 启动连续检测失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[检测管理器] 处理连续检测逻辑失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置检测周期状态（线程安全）
        /// </summary>
        public void Reset()
        {
            // 🔧 移除锁：工业控制中检测流程是顺序的，不需要锁保护
            _2DCompleted = false;
            _3DCompleted = false;
            // 🔧 关键修复：不要重置配置状态_3DEnabled！这是用户配置的状态，不应该被重置
            // _3DEnabled = false; // 移除这行，保持用户配置的3D使能状态

            // 🔧 新增：停止2D超时定时器
            Stop2DTimeoutTimer();

            LogManager.Info("[检测管理器] 检测周期状态已重置，准备下次检测周期");
        }

        /// <summary>
        /// 初始化系统检测管理器（软件启动时调用）
        /// </summary>
        public void InitializeSystem()
        {
            // 🔧 移除锁：直接操作
            _isSystemInitialized = true;
            // 软件启动默认进入模板配置模式：启动后会自动加载一次模板
            _systemState = SystemDetectionState.TemplateConfiguring;
            _currentDetectionMode = DetectionMode.Full; // 默认全检测模式

            LogManager.Info($"[检测管理器] ✅ 系统检测管理器已初始化");
            LogManager.Info($"[检测管理器] 检测模式: {_currentDetectionMode}");
            LogManager.Info($"[检测管理器] 系统状态: {_systemState}");

            // 软件启动时自动启动检测周期
            bool shouldEnable3D = _page1Instance?.Is3DDetectionEnabled() ?? false;
            StartDetectionCycle(shouldEnable3D);
            LogManager.Info($"[检测管理器] 🚀 系统启动时自动启动检测周期，3D启用: {shouldEnable3D}");
        }

        /// <summary>
        /// 设置检测模式
        /// </summary>
        public bool SetDetectionMode(DetectionMode mode)
        {
            // 🔧 移除锁：直接操作
            if (_systemState == SystemDetectionState.Detecting || _systemState == SystemDetectionState.Processing)
            {
                LogManager.Warning($"[检测管理器] ⚠️ 检测进行中，无法切换检测模式");
                return false;
            }

            var oldMode = _currentDetectionMode;
            _currentDetectionMode = mode;

            LogManager.Info($"[检测管理器] 检测模式已切换: {oldMode} → {mode}");
            return true;
        }

        /// <summary>
        /// 设置系统状态（用于相机调节等特殊场景）
        /// </summary>
        public void SetSystemState(SystemDetectionState state)
        {
            // 🔧 移除锁：直接操作
            if (_systemState == state)
            {
                return;
            }

            var oldState = _systemState;
            _systemState = state;

            LogManager.Info($"[检测管理器] 系统状态已切换: {oldState} → {state}");
        }

        /// <summary>
        /// 检查是否允许处理检测结果
        /// </summary>
        public bool ShouldProcessDetection()
        {
            // 🔧 移除锁：简单的状态检查不需要锁保护
            if (!_isSystemInitialized)
            {
                LogManager.Warning("[检测管理器] 系统未初始化，不处理检测");
                return false;
            }

            if (_currentDetectionMode == DetectionMode.Disabled)
            {
                LogManager.Info("[检测管理器] 检测模式已禁用，不处理检测");
                return false;
            }

            if (_systemState == SystemDetectionState.CameraAdjusting || _systemState == SystemDetectionState.Maintenance)
            {
                LogManager.Info($"[检测管理器] 系统处于特殊状态({_systemState})，不处理检测");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 启动系统测试模式
        /// </summary>
        /// <param name="enable3D">是否启用3D检测</param>
        public void StartSystemTestMode(bool enable3D)
        {
            // 🔧 移除锁：直接操作
            LogManager.Info($"[检测管理器] 启动系统测试模式 - 3D启用: {enable3D} (shield={ThreeDSettings.Is3DShielded})");

            // 设置系统测试模式
            _currentDetectionMode = DetectionMode.SystemTest;
            _systemState = SystemDetectionState.SystemTesting;

            // 启动检测周期
            _2DCompleted = false;
            _3DCompleted = false;
            _3DEnabled = enable3D && !ThreeDSettings.Is3DShielded;

            Page1.ResetCached2DDetectionResult();
            Page1.ResetCached3DDetectionResult();

            LogManager.Info($"[检测管理器] 系统测试模式已启动，等待检测完成");
        }

        /// <summary>
        /// 停止系统测试模式，恢复正常模式
        /// </summary>
        public void StopSystemTestMode()
        {
            // 🔧 移除锁：直接操作
            LogManager.Info("[检测管理器] 停止系统测试模式，恢复正常检测模式");

            // 恢复正常模式
            _currentDetectionMode = DetectionMode.Full;
            _systemState = SystemDetectionState.WaitingForTrigger;

            // 重置状态
            _2DCompleted = false;
            _3DCompleted = false;

            LogManager.Info("[检测管理器] 已恢复正常检测模式");
        }

        /// <summary>
        /// 启动2D超时检测定时器（3D完成后2秒）
        /// </summary>
        private void Start2DTimeoutTimer()
        {
            // 如果2D已经完成了，就不需要启动超时检测
            if (_2DCompleted)
            {
                LogManager.Info("[检测管理器] 2D已完成，无需启动超时定时器");
                return;
            }

            // 先停止之前的定时器（如果有）
            Stop2DTimeoutTimer();

            LogManager.Info("[检测管理器] 启动2D超时定时器（2秒后检查）");

            // 启动新的定时器，2秒后触发
            _2DTimeoutTimer = new System.Threading.Timer(
                callback: (state) => Handle2DTimeout(),
                state: null,
                dueTime: 2000, // 2秒后触发
                period: System.Threading.Timeout.Infinite // 只触发一次
            );
        }

        /// <summary>
        /// 停止2D超时检测定时器
        /// </summary>
        private void Stop2DTimeoutTimer()
        {
            if (_2DTimeoutTimer != null)
            {
                _2DTimeoutTimer.Dispose();
                _2DTimeoutTimer = null;
                LogManager.Info("[检测管理器] 2D超时定时器已停止");
            }
        }

        /// <summary>
        /// 处理2D超时情况
        /// </summary>
        private void Handle2DTimeout()
        {
            // 检查2D是否真的还没完成
            if (_2DCompleted)
            {
                LogManager.Info("[检测管理器] 2D在超时前已完成，无需超时处理");
                return;
            }

            LogManager.Warning("[检测管理器] ⚠️ 2D检测超时！3D已完成2秒，但2D仍未完成");

            // 标记2D已完成（避免后续算法回调再次触发判定）
            _2DCompleted = true;

            // 设置2D检测结果为"2D超时"
            Page1.SetCached2DDetectionResult("2D超时");
            LogManager.Info("[检测管理器] 已设置2D检测结果为'2D超时'");

            // 在UI线程执行统一判定和错误复位
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                try
                {
                    // 首先执行统一判定和IO，更新DefectType和饼图
                    _page1Instance?.ExecuteUnifiedJudgementAndIO();
                    LogManager.Info("[检测管理器] 已执行统一判定和IO，DefectType已更新为'2D超时'");

                    // 立即执行错误复位（无延迟）
                    _page1Instance?.ExecuteErrorResetWithoutDialog();
                    LogManager.Info("[检测管理器] 已自动触发错误复位（无弹窗）");
                }
                catch (Exception ex)
                {
                    LogManager.Error($"[检测管理器] 执行统一判定或错误复位失败: {ex.Message}");
                }
            }));
        }
    }

    /// <summary>
    /// 检测模式枚举：定义系统支持的检测模式
    /// </summary>
    public enum DetectionMode
    {
        Disabled,       // 全都不检测（调试模式、参数调节时）
        Only2D,         // 仅检测2D
        Only3D,         // 仅检测3D
        Full,           // 全检测（2D + 3D）
        Paused,         // 暂停检测（保持状态但不处理新检测）
        SystemTest      // 系统测试模式（需要记录性能数据）
    }

    /// <summary>
    /// 系统检测状态枚举：定义系统当前的运行状态
    /// </summary>
    public enum SystemDetectionState
    {
        Idle,               // 空闲状态
        WaitingForTrigger,  // 等待触发
        Detecting,          // 检测中
        Processing,         // 处理结果中
        CameraAdjusting,    // 相机调节中（禁止检测）
        Maintenance,        // 维护模式（禁止检测）
        TemplateConfiguring,// 模板配置模式（允许检测但不统计）
        SystemTesting       // 系统测试模式（记录性能数据）
    }
}
