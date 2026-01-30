# Skill 草案（请勿启用）

名称: GlueInspect 平台工业项目集成（草案）
状态: draft
文件: skill_开发中.md
范围: PlatformHost.Wpf + Platform 插件框架集成

## 目的
提供一套可复用流程，把 GlueInspect Platform 框架落地到工业项目：定义所需输入、映射参数、集成算法引擎/插件，并完成 IO/PLC 输出联动。

## 触发条件（仅手动）
仅在用户明确要求“基于该框架进行工业项目集成”并要求“生成/更新 skill”时使用。

## 必要输入
- 项目元信息：样品类型、涂布类型、步骤清单、缺陷分类
- 算法路线：插件模式（Platform/）或 WPF 引擎（PlatformHost.Wpf/Algorithms）
- 参数规格：参数名、单位、范围、默认值、换算规则
- 输出规格：指标名、上下限、OK/NG 判定逻辑、缺陷命名
- 硬件信息：IO/PLC 型号、映射、时序、部署要求
- 验收条件：性能约束、验证标准

## 预期输出
- 引擎/插件映射更新
- 步骤/参数注册与模板更新
- IO/PLC 映射与运行时处理更新
- 验证步骤与必需资源说明

## 关键路径（项目相关）
- 基座项目: `E:\posen_project\点胶检测\上位机程序\WpfApp2`
- 平台根目录: `E:\posen_project\点胶检测\上位机程序\WpfApp2\Platform`
- Abstractions: `src/GlueInspect.Platform.Abstractions/`
- Runtime Loader: `src/GlueInspect.Platform.Runtime/`
- WPF 宿主: `PlatformHost.Wpf/`
- 算法引擎（WPF）: `PlatformHost.Wpf/Algorithms/`
- 通用相机接口: `PlatformHost.Wpf/Hardware/`（GenericCameraManager/Models）
- 通用相机配置文件: `PlatformHost.Wpf/Config/GenericCameraProfiles.json`
- 图像渲染器: `PlatformHost.Wpf/Rendering/`（项目级选择，Renderer.json）
- 图像查看器: `PlatformHost.Wpf/UI/Controls/ImageInspectionViewer.*`（缩放/平移/坐标RGB/像素级显示）
- UI 流程: `PlatformHost.Wpf/UI/`
- 模板目录: `PlatformHost.Wpf/Templates/`（运行时生成）
- 配置目录: `PlatformHost.Wpf/Config/`
- 参数图片: `PlatformHost.Wpf/Resources/ParameterImages/`
- IO/PLC: `PlatformHost.Wpf/SMTGPIO/`

## 核心数据流
1) UI/模板 -> AlgorithmInput（Page1.BuildAlgorithmInput + PopulateAlgorithmInputParameters）
2) 引擎/插件执行 -> AlgorithmResult
3) 结果归一化 + 回填 UI/统计
4) 可选 IO/PLC 输出

## 必改触点（函数/文件）

### 引擎选择与注册
- `PlatformHost.Wpf/Algorithms/AlgorithmEngineRegistry.cs`
  - Initialize() 注册引擎
  - GetDefaultDescription() 定义 UI 提示
- `PlatformHost.Wpf/Algorithms/OpenCvOnnxAlgorithmEngine.cs`
  - OpenCV + ONNX 组合引擎（经典算法 + 深度学习）

### 渲染器选择（项目级）
- `PlatformHost.Wpf/Rendering/RendererSettingsManager.cs`
- `PlatformHost.Wpf/Rendering/ImageRendererManager.cs`
- `PlatformHost.Wpf/Rendering/VmImageRenderer.cs`
- `PlatformHost.Wpf/Rendering/FileImageRenderer.cs`
- `PlatformHost.Wpf/UI/Controls/ImageInspectionViewer.xaml(.cs)`
- `PlatformHost.Wpf/UI/Models/AlgorithmEngineSettings.cs`
  - PreferredEngineId + AlgorithmEngine.json

### 算法输入映射
- `PlatformHost.Wpf/UI/Page1.xaml.cs`
  - BuildAlgorithmInput(...)
  - PopulateAlgorithmInputParameters(...)

### 算法结果映射
- `PlatformHost.Wpf/UI/Page1.xaml.cs`
  - ExecuteAlgorithmEngineDetectionAsync(...)
  - NormalizeAlgorithmResult(...)
  - ApplyAlgorithmResultTo2DCache(...)
  - BuildAlgorithmResult(...)

### 步骤/参数注册
- `PlatformHost.Wpf/UI/Models/ModuleRegistry.cs`
  - RegisterAllDefaultModules() 定义步骤与参数
- `PlatformHost.Wpf/UI/Models/ModuleDefinition.cs`
  - 参数换算与映射
- `PlatformHost.Wpf/UI/Models/Class1.cs`
  - StepType / SampleType / CoatingType / TemplateParameters

### IO/PLC
- `PlatformHost.Wpf/SMTGPIO/IOManager.cs`
  - Initialize(), SetDetectionResult(...)
- `PlatformHost.Wpf/SMTGPIO/PLCSerialController.cs`
  - PLC 通讯细节
- `PlatformHost.Wpf/App.xaml.cs`
  - 启动/退出初始化与释放

### 插件路线（可选）
- `src/GlueInspect.Platform.Abstractions/IAlgorithmPlugin.cs`
- `src/GlueInspect.Platform.Abstractions/IAlgorithmSession.cs`
- `src/GlueInspect.Platform.Runtime/PluginLoader.cs`
- `src/GlueInspect.Platform.Runtime/AlgorithmRegistry.cs`

## 交付物清单
- [ ] 新/改 SampleType + CoatingType + StepType
- [ ] ModuleRegistry 步骤映射（输入/输出/动作）
- [ ] 模板 JSON（每个产线模板）
- [ ] 算法引擎或插件实现 + 注册
- [ ] 输出指标与 UI/统计对齐
- [ ] IO/PLC 映射更新并实机验证
- [ ] Release 构建 + 手动回归

## 需要向用户确认的问题
1) 选择路线：WPF 引擎还是 Platform 插件（或两者）？
2) 完整步骤清单与参数定义？
3) 输出指标与 OK/NG 规则？
4) 硬件型号与 IO/PLC 映射？
5) 性能与验收约束？
6) 模板 JSON 由谁维护？

## 非目标
- 不改 UI 结构
- 不新增业务页面
- 不更改无关 VM 逻辑（除非必须）

## 验证步骤（手动）
- Release 构建并运行 GlueInspect.exe
- 验证 TemplateConfigPage + Page1 全流程
- 确认 AlgorithmEngine.json 生效
- 参数说明与图片映射正确
- 图片选择 -> 下一项 -> 返回，图像仍显示
- 图像窗口可缩放/拖拽，鼠标位置显示坐标与 RGB
- IO/PLC 实机信号正确

## 备注
- 保留原有中文注释，不随意删除。
- 大体量资源放 contents/ 或 ImageTemp/。
- 避免改动无关文件或 UI 流程。
