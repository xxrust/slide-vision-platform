**Counts**
- 全量文件（含 `bin/obj/packages/out/.git` 等生成与依赖目录）：7714
- `rg --files` 统计（遵循忽略规则）：970
- 纯源码/配置/文档（剔除 `*/bin`, `*/obj`, `packages`, `out`, `.git`）：282

**Grouping（按“功能边界 + 降低上下文”分组）**
- 平台核心（插件接口 + 运行时加载）：包含 `src/Slide.Platform.Abstractions/` 与 `src/Slide.Platform.Runtime/` 全部文件；关键文件 `src/Slide.Platform.Abstractions/IAlgorithmPlugin.cs:1`, `src/Slide.Platform.Abstractions/IAlgorithmSession.cs:1`, `src/Slide.Platform.Abstractions/AlgorithmInput.cs:1`, `src/Slide.Platform.Abstractions/AlgorithmResult.cs:1`, `src/Slide.Platform.Abstractions/AlgorithmDescriptor.cs:1`, `src/Slide.Platform.Runtime/PluginLoader.cs:1`, `src/Slide.Platform.Runtime/AlgorithmRegistry.cs:1`, `src/Slide.Platform.Runtime/PluginLoadResult.cs:1`；理由：平台稳定接口层 + 插件加载机制。
- WPF 宿主应用（业务/交互/硬件/算法引擎）：包含 `PlatformHost.Wpf/` 代码与配置（剔除 `bin/obj`）；关键文件 `PlatformHost.Wpf/MainWindow.xaml.cs:1`, `PlatformHost.Wpf/UI/Page1.xaml.cs:1`, `PlatformHost.Wpf/UI/ConfigPage.xaml.cs:1`, `PlatformHost.Wpf/UI/TemplateConfigPage.xaml.cs:1`, `PlatformHost.Wpf/UI/Models/TemplateHierarchyConfig.cs:1`, `PlatformHost.Wpf/UI/Models/ModuleRegistry.cs:1`, `PlatformHost.Wpf/UI/Models/Class1.cs:1`, `PlatformHost.Wpf/UI/ImageSourceNaming.cs:1`, `PlatformHost.Wpf/Algorithms/AlgorithmEngineRegistry.cs:1`, `PlatformHost.Wpf/Algorithms/Slide.Algorithm.Contracts/AlgorithmContracts.cs:1`, `PlatformHost.Wpf/Algorithms/Slide.Algorithm.OpenCV/OpenCvAlgorithmEngine.cs:1`, `PlatformHost.Wpf/Algorithms/OpenCvOnnxAlgorithmEngine.cs:1`, `PlatformHost.Wpf/Rendering/ImageRendererContracts.cs:1`, `PlatformHost.Wpf/Rendering/FileImageRenderer.cs:1`, `PlatformHost.Wpf/ThreeD/ThreeDService/IThreeDService.cs:1`, `PlatformHost.Wpf/ThreeD/ThreeDService/NamedPipeThreeDService.cs:1`, `PlatformHost.Wpf/Hardware/GenericCameraManager.cs:1`, `PlatformHost.Wpf/Config/TemplateHierarchy.json:1`；理由：宿主层承担 UI、配置、检测流程、硬件与 3D。
- 插件示例（插件侧算法实现）：`plugins/Slide.Algorithm.OpenCv/OpenCvAlgorithmPlugin.cs:1`, `plugins/Slide.Algorithm.Onnx/OnnxAlgorithmPlugin.cs:1`；理由：插件边界清晰、与平台核心对接。
- 示例工程（演示插件加载与运行）：`samples/Slide.Algorithm.Sample/SampleAlgorithmPlugin.cs:1`, `samples/Slide.Platform.SampleHost/Program.cs:1`；理由：演示端到端插件运行路径。
- 文档/流程说明：`README.md:1`, `CLAUDE.md:1`, `CODEX.md:1`, `decompose.md:1`, `summarize.md:1`, `prd.json:1`；理由：规范与愿景集中。
- 生成物/外部依赖：`packages/`, `out/`, `*/bin/`, `*/obj/`；理由：体量大且非业务源代码，分析时独立放置。

**Multi-Agent Notes（并行分工记录）**
- Agent A（愿景与架构概览）：分读 `README.md:1` 与 `CLAUDE.md:1`，确认“平台/算法/业务分离”目标、双层架构和插件开发流程。
- Agent B（平台核心 + 示例宿主）：分读 `src/Slide.Platform.Abstractions/IAlgorithmPlugin.cs:1`, `src/Slide.Platform.Abstractions/IAlgorithmSession.cs:1`, `src/Slide.Platform.Runtime/PluginLoader.cs:1`, `src/Slide.Platform.Runtime/AlgorithmRegistry.cs:1`, `samples/Slide.Platform.SampleHost/Program.cs:1`, `samples/Slide.Algorithm.Sample/SampleAlgorithmPlugin.cs:1`，确认插件加载、注册、执行与示例宿主调用路径。
- Agent C（模板与配置流程）：分读 `PlatformHost.Wpf/UI/Models/TemplateHierarchyConfig.cs:1`, `PlatformHost.Wpf/Config/TemplateHierarchy.json:1`, `PlatformHost.Wpf/UI/Models/ModuleRegistry.cs:1`, `PlatformHost.Wpf/UI/TemplateConfigPage.xaml.cs:1`, `PlatformHost.Wpf/UI/ConfigPage.xaml.cs:1`, `PlatformHost.Wpf/UI/Models/Class1.cs:1`, `PlatformHost.Wpf/UI/ImageSourceNaming.cs:1`, `PlatformHost.Wpf/MainWindow.xaml.cs:1`，确认模板档案驱动步骤、配置页面与模板加载流程。
- Agent D（宿主算法/渲染/硬件/3D）：分读 `PlatformHost.Wpf/Algorithms/AlgorithmEngineRegistry.cs:1`, `PlatformHost.Wpf/Algorithms/Slide.Algorithm.Contracts/AlgorithmContracts.cs:1`, `PlatformHost.Wpf/Algorithms/Slide.Algorithm.OpenCV/OpenCvAlgorithmEngine.cs:1`, `PlatformHost.Wpf/Algorithms/OpenCvOnnxAlgorithmEngine.cs:1`, `PlatformHost.Wpf/Rendering/ImageRendererContracts.cs:1`, `PlatformHost.Wpf/Rendering/FileImageRenderer.cs:1`, `PlatformHost.Wpf/ThreeD/ThreeDService/IThreeDService.cs:1`, `PlatformHost.Wpf/ThreeD/ThreeDService/NamedPipeThreeDService.cs:1`, `PlatformHost.Wpf/Hardware/GenericCameraManager.cs:1`，确认引擎注册、渲染抽象、3D IPC 与相机配置管理。

**Framework Diagram**
```
[Platform Core]
  Abstractions (Plugin API) + Runtime (Loader/Registry)
        |
        | loads
        v
  [Algorithm Plugins] ----> [Sample Host (Console)]
        |
        | (separate line)
        v
[WPF Host App]
  UI (Page1/Config/Template) -> Template Profiles -> Step Modules
  Algorithm Engines (OpenCV/ONNX/Composite)
  Rendering (ImageRenderer)
  Hardware (Cameras/PLC/GPIO)
  3D Service (NamedPipe IPC) <-> 3D Host Process
```

**Components (what each part does)**
- 插件接口与数据模型：定义插件入口和会话运行契约 `src/Slide.Platform.Abstractions/IAlgorithmPlugin.cs:1`, `src/Slide.Platform.Abstractions/IAlgorithmSession.cs:1`, `src/Slide.Platform.Abstractions/AlgorithmInput.cs:1`, `src/Slide.Platform.Abstractions/AlgorithmResult.cs:1`.
- 插件加载与注册：按目录扫描并实例化插件 `src/Slide.Platform.Runtime/PluginLoader.cs:1`, `src/Slide.Platform.Runtime/AlgorithmRegistry.cs:1`.
- 示例宿主：加载插件并执行算法 `samples/Slide.Platform.SampleHost/Program.cs:1`.
- WPF 宿主入口与页面框架：窗口和页面切换/初始化 `PlatformHost.Wpf/MainWindow.xaml.cs:1`.
- 模板档案与步骤配置：模板定义/映射 `PlatformHost.Wpf/UI/Models/TemplateHierarchyConfig.cs:1`, JSON 配置 `PlatformHost.Wpf/Config/TemplateHierarchy.json:1`, 步骤定义与注册 `PlatformHost.Wpf/UI/Models/ModuleRegistry.cs:1`, 模板参数结构 `PlatformHost.Wpf/UI/Models/Class1.cs:1`.
- 模板配置与入口 UI：配置页与模板流程 `PlatformHost.Wpf/UI/ConfigPage.xaml.cs:1`, `PlatformHost.Wpf/UI/TemplateConfigPage.xaml.cs:1`, 图像源命名 `PlatformHost.Wpf/UI/ImageSourceNaming.cs:1`.
- 算法引擎系统：引擎注册与选择 `PlatformHost.Wpf/Algorithms/AlgorithmEngineRegistry.cs:1`, 引擎契约 `PlatformHost.Wpf/Algorithms/Slide.Algorithm.Contracts/AlgorithmContracts.cs:1`, OpenCV 引擎 `PlatformHost.Wpf/Algorithms/Slide.Algorithm.OpenCV/OpenCvAlgorithmEngine.cs:1`, OpenCV+ONNX 组合引擎 `PlatformHost.Wpf/Algorithms/OpenCvOnnxAlgorithmEngine.cs:1`.
- 渲染系统：渲染接口与实现 `PlatformHost.Wpf/Rendering/ImageRendererContracts.cs:1`, `PlatformHost.Wpf/Rendering/FileImageRenderer.cs:1`.
- 3D 服务：主进程抽象与 IPC 客户端 `PlatformHost.Wpf/ThreeD/ThreeDService/IThreeDService.cs:1`, `PlatformHost.Wpf/ThreeD/ThreeDService/NamedPipeThreeDService.cs:1`.
- 硬件抽象：通用相机配置与持久化 `PlatformHost.Wpf/Hardware/GenericCameraManager.cs:1`.

**Cohesion/Coupling Gaps（未做到高内聚低耦合的点）**
- 主界面承担过多职责（UI、检测流程、3D、硬件、数据、日志等混杂），导致“巨型类/强耦合”风险：`PlatformHost.Wpf/UI/Page1.xaml.cs:1`.
- 引擎注册依赖 UI 静态实例并隐式初始化，算法层与 UI 互相耦合：`PlatformHost.Wpf/Algorithms/AlgorithmEngineRegistry.cs:1`.
- 平台核心插件接口与宿主算法契约并存，形成双模型并行（容易分叉）：`src/Slide.Platform.Abstractions/IAlgorithmPlugin.cs:1` vs `PlatformHost.Wpf/Algorithms/Slide.Algorithm.Contracts/AlgorithmContracts.cs:1`.
- 模板配置/步骤注册高度集中于静态注册表，业务逻辑、UI 参数、单位换算与流程编排绑定在一起：`PlatformHost.Wpf/UI/Models/ModuleRegistry.cs:1`.
- 模板档案/图像源解析直接依赖 UI 单例与文件路径，耦合运行时状态：`PlatformHost.Wpf/UI/ImageSourceNaming.cs:1`.
- 模板参数模型承载业务枚举/算法引擎选择与序列化兼容逻辑，职责混杂：`PlatformHost.Wpf/UI/Models/Class1.cs:276`.
- 全局变量/单例管理器广泛存在，依赖注入与模块边界不清：`PlatformHost.Wpf/Algorithms/AlgorithmGlobalVariables.cs:1`, `PlatformHost.Wpf/UI/Models/TemplateHierarchyConfig.cs:1`.

**PRD（基于现有架构与需求分析）**
- 背景：构建通用视觉检测平台，实现“平台/算法/业务”分离，提高算法复用与项目交付速度。
- 目标：插件化算法接入、模板档案驱动的配置流程、统一的检测执行与结果展示、可扩展硬件/3D 集成。
- 非目标：替换现有硬件驱动栈、重写现有业务算法实现。
- 角色：算法工程师（开发插件）、应用工程师（配置模板/流程）、产线操作员（运行检测/查看结果）、系统集成工程师（设备/3D/配置落地）。
- 关键用户故事：插件算法安装与管理、模板档案创建/加载、步骤参数配置、批量检测与结果导出、3D/硬件可用性检测与故障提示。
- 功能需求：插件扫描与注册、模板档案与步骤编排、算法引擎选择与执行、渲染与结果展示、硬件配置与状态监控、3D IPC 与失败降级策略。
- 非功能需求：启动与检测性能、可观测性（日志/错误码）、配置可迁移性、插件隔离性与兼容性。
- 数据与配置：模板档案、参数配置、设备配置、检测记录与导出规则。
- 里程碑：MVP（插件+模板+检测闭环）→ 硬件/3D 稳定化 → 插件市场化与生态。
- 风险：UI 与算法耦合过深导致扩展成本高、模板与步骤注册过于集中导致改动代价大、双契约体系导致集成分裂。
- 关键指标：插件接入周期、模板创建时间、单次检测时延、故障率、跨项目复用率。

如果你要我继续深入，我可以：
1) 输出“剔除生成目录”的完整文件清单（按组+清单）  
2) 基于当前架构生成正式 UML/组件图  
3) 把 PRD 落成 `prd.md`/`prd.json` 并补充可执行验收标准
