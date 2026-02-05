# PRD - Platform 项目框架分析（多 Agent 版）

- 状态：草案
- 版本：1.0
- 负责人：待定
- 目标发布日期：待定

## 1. 背景与目的
项目规模大、模块多，单人一次性阅读成本高。需要通过结构化梳理与多 Agent 并行分工，输出“模块关系 + 数据流 + 改动风险”的框架分析文档，帮助使用者快速理解项目并安全修改。

## 2. 目标
- 识别并清晰划分项目模块边界与职责。
- 给出平台插件体系与宿主算法体系的关系与差异。
- 输出关键数据流与依赖图，方便定位修改入口。
- 列出耦合与修改风险，降低回归成本。
- 提供可复用的多 Agent 分工与汇总模板。

## 3. 非目标
- 不进行代码重构或功能新增。
- 不替换现有硬件/3D 依赖栈。
- 不执行构建、测试或发布流程。

## 4. 范围与模块分组
- 平台核心：插件接口与运行时加载机制。
- 插件实现：OpenCV/ONNX 插件与示例插件。
- 示例宿主：控制台加载与执行插件。
- WPF 宿主应用：UI/模板/算法引擎/渲染/硬件/3D。
- 配置与文档：配置文件与架构说明文档。
- 生成物与外部依赖：bin/obj/packages/out 等（仅做隔离说明）。

## 5. 架构概览（文本图）
```
[Platform Core (netstandard2.0)]
  Abstractions (IAlgorithmPlugin/IAlgorithmSession/Input/Result)
  Runtime (PluginLoader/Registry)
        | loads
        v
  [Algorithm Plugins] ---> [Sample Host (Console)]

[WPF Host (.NET Framework 4.8)]
  TemplateHierarchy.json -> TemplateHierarchyConfig -> ImageSourceNaming
  Page1 (UI + Pipeline) -> Build AlgorithmInput -> AlgorithmEngineRegistry
  Engines (OpenCV / ONNX / OpenCV+ONNX)
  Rendering (IImageRenderer/FileImageRenderer)
  Hardware (GenericCameraManager)
  3D Service (NamedPipe) <-> 3D Host Process (Keyence)
```

## 6. 关键数据流
- 平台插件流：目录扫描 -> 实例化插件 -> 注册 -> 创建会话 -> Run 输入输出。
- 宿主检测流：用户选图 -> 构建 AlgorithmInput -> 选引擎 -> 执行 -> 渲染/3D 并行。
- 模板与图像源流：TemplateHierarchy.json -> Profile -> ImageSources -> 输入路径映射。

## 7. 模块职责说明（精简）
- 平台抽象层：定义插件协议与输入输出模型，保障算法可插拔。
- 平台运行时：按目录扫描插件、实例化并注册。
- 插件实现：以算法为中心实现插件契约，供平台加载。
- 示例宿主：演示插件加载/执行流程，作为验证入口。
- WPF UI：模板配置、检测入口、结果展示与流程编排。
- 宿主算法引擎：提供 OpenCV/ONNX/组合引擎并执行检测。
- 渲染系统：将检测图像与结果渲染到 UI。
- 3D 子系统：主进程通过 IPC 控制独立 3D Host 进程。
- 硬件子系统：相机/设备配置管理与持久化。

## 8. 关键耦合与修改风险
- 平台插件契约与宿主算法契约并存，未来统一需要桥接层。
- AlgorithmEngineRegistry 依赖 UI 静态实例，算法层与 UI 耦合。
- Page1 聚合过多职责（UI/流程/状态/检测），修改回归风险高。
- 模板 Profile 与图像源直接影响算法输入构建，配置改动需联动流程。
- 3D 逻辑依赖 IPC 及授权检查，修改易引发运行时连接失败。
- 配置文件与默认值分散在多处，修改需注意一致性。

## 9. 需求与交付物
- 模块分组清单与分组理由。
- 多 Agent 阅读范围与关键发现记录。
- 架构关系图（文本/ASCII）。
- 关键数据流说明。
- 耦合问题与修改风险清单。
- 本 PRD 文档（可复用模板）。

## 10. 验收标准
- 模块边界覆盖平台核心、宿主层、插件、示例、配置、外部依赖。
- 至少 5 条明确的耦合/风险说明。
- 数据流清晰，能定位主要改动入口。
- PRD 可直接复用到后续项目框架分析。

## 11. 多 Agent 并行拆分建议
- Agent A：平台核心 + 插件体系（接口/加载/示例宿主）。
- Agent B：WPF 宿主主流程（Page1 + 输入/输出链路）。
- Agent C：模板与配置系统（Profile/参数/配置页）。
- Agent D：算法引擎 + 渲染系统（Contracts/Engines/Renderer）。
- Agent E：3D/硬件/外设（NamedPipe 3D + Camera/PLC/GPIO）。

## 12. Agent 输出模板（建议统一）
- 阅读范围：目录/文件清单。
- 模块职责：2-5 条摘要。
- 数据流：1-2 条关键路径。
- 改动入口：常见修改点与入口文件。
- 风险点：影响面与潜在回归。

