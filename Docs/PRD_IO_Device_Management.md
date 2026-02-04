# PRD — IO 设备管理模块（PC IO 口）

## 1. 背景与目标

当前 PC 的 IO 口配置与操作分散在“硬件配置”页面与 `SMTGPIO` 相关类中，配置入口不统一、设备复用能力弱，且与“设备管理模块”脱节。需要将 IO 口作为一种设备纳入设备管理模块，实现配置、连接、测试、调用的统一入口，并保证现有业务调用（IO 输出/检测结果输出）不受影响。

目标：
- 将 IO 口设备配置迁移到“设备管理”并持久化到配置文件。
- 支持选择 IO 设备品牌/型号与端口号（Port），并能连接/断开/测试。
- 保留并兼容现有 IO 输出调用（如 `IOManager.SetDetectionResult`）。
- 提供可视化 IO 测试窗口（读取/写入/复位），从设备管理的“测试”入口打开。

非目标：
- 不改变 IO 板卡驱动层（`SMTGPIOController`）的实现。
- 不引入数据库持久化（仍使用配置文件）。
- 不新增复杂的多设备并发调度或硬件诊断功能。

---

## 2. 当前现状梳理（需要迁移的 IO 相关入口）

- **IO 业务调用**：`SMTGPIO/IOManager.cs` 提供初始化与 IO 输出控制（OK/NG、单点输出、复位）。
- **配置入口**：`UI/GPIOSettingsWindow.xaml(.cs)` 支持选择 `SMTGPIODeviceType` 与 `Port`，配置落在 `config/gpio_config.json`。
- **UI 位置**：`UI/HardwareConfigPage` 右侧 IO 控制区（包含 IO 指示灯与按钮）。
- **业务调用点**：`Page1.xaml.cs` 与 `SmartAnalysisWindowManager` 等直接调用 `IOManager`。

需要将配置入口与测试入口迁移到“设备管理”，并明确后续调用路径：业务仍调用 `IOManager`，但其配置来源从设备管理的设备配置中读取。

---

## 3. 用户角色

- 操作员：查看 IO 状态、执行简单 IO 测试。
- 工程师：配置 IO 设备（型号/端口）、验证连通性。
- 系统集成方：通过统一接口触发 IO 输出（OK/NG、报警等）。

---

## 4. 用户故事与验收标准

### US-001 配置 IO 设备（设备管理）
**描述：** 作为工程师，我希望在设备管理中配置 IO 设备型号与端口号。  
**验收标准：**
- [ ] 设备管理新增“IO 设备”类型（或硬件类型=IO）。
- [ ] 可选择 IO 设备型号（`SMTGPIODeviceType`）与端口号（1-8）。
- [ ] 配置保存到配置文件，且重启后可自动加载。

### US-002 IO 测试入口
**描述：** 作为工程师，我希望在设备管理中选择 IO 设备后打开测试窗口。  
**验收标准：**
- [ ] 在设备管理“测试”入口打开 IO 测试窗口。
- [ ] 测试窗口显示设备信息（品牌/型号/端口）。
- [ ] 支持连接/断开、单点输出、复位、读状态。

### US-003 业务调用兼容
**描述：** 作为系统集成方，我希望现有 IO 输出调用继续工作。  
**验收标准：**
- [ ] `IOManager.SetDetectionResult`、`SetSingleOutput` 等调用不需要修改业务逻辑。
- [ ] `IOManager.Initialize` 从设备管理配置加载设备型号与端口。
- [ ] 若未配置 IO 设备，保留合理的错误提示与安全降级。

---

## 5. 功能需求

- FR-1 在设备管理中支持 IO 设备配置（型号/端口）。
- FR-2 设备配置持久化到配置文件（建议复用 `Config/DeviceManagement.json`）。
- FR-3 IO 测试窗口：连接/断开/输出/复位/状态查询。
- FR-4 `IOManager` 初始化逻辑改为读取设备管理配置（IO 设备）。
- FR-5 迁移或废弃 `GPIOSettingsWindow` 的入口（避免重复配置入口）。

---

## 6. 数据模型建议

**DeviceConfig（扩展）**
- ProtocolType: `Serial/TcpIp`（现有）
- HardwareName: `IO`（新）
- Brand: 设备品牌（如 Advantech/Leadshine/SMT）
- IoOptions: {
  - DeviceType: `SMTGPIODeviceType`
  - Port: `uint`
}

> 备注：`DeviceType` 可与 `SMTGPIODeviceType` 一致，配置字段与 `GPIOConfig` 对齐。

---

## 7. 交互与界面（建议）

- 设备管理列表：显示 IO 设备与状态。
- 新增/编辑 IO 设备：设备型号下拉（来自 `SMTGPIODeviceType`），端口号输入。
- 测试窗口：
  - 状态指示（连接/断开）
  - IO 输出控制（O0~O3 或配置化通道）
  - 状态读取（刷新/轮询）

---

## 8. 技术方案建议

- `DeviceManager` 新增 `IoDeviceClient`：封装对 `IOManager` / `SMTGPIOController` 的访问。
- `IOManager` 初始化配置来源：
  - 优先读取设备管理中的 IO 设备配置；
  - 若为空则回退到旧 `gpio_config.json`（兼容老项目）。
- 测试窗口与 `IOManager` 的关系：
  - 测试窗口只操作当前选中的 IO 设备；
  - 连接/断开不影响其他业务流程（需有提示）。

---

## 9. 测试策略

- 配置保存/加载：新增 IO 设备后重启验证配置是否加载。
- IO 测试：
  - 连接成功后可执行单点输出与复位。
  - 输出状态刷新正确显示。
- 业务回归：执行检测流程时 IO 输出仍按 `SetDetectionResult` 工作。

---

## 10. 风险与开放问题

- IO 设备是否允许多实例？如果只允许单实例，需要在设备管理层限制。
- 现有 `GPIOSettingsWindow` 是否保留为兼容入口，还是完全移除？
- IO 输出通道数量是否固定 4 路，还是需要支持可配置通道数？

---

> 若 PRD 认可，可拆分为设备模型扩展、IO 配置迁移、测试窗口、业务调用兼容等子任务。
