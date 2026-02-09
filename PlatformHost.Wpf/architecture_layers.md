# Platform 平面架构图 — 按接口耦合边界拆分

> 生成日期：2026-02-06
> 阅读方式：从上到下 = 从外部调用者到底层实现；同一层级的块互不直接依赖

---

## 总览

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          WPF 宿主 (PlatformHost.Wpf)                            │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐ ┌──────────┐            │
│  │  UI 层   │ │ 管线编排 │ │ 渲染系统 │ │ 硬件抽象  │ │ 配置系统 │            │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └─────┬─────┘ └────┬─────┘            │
│       │             │            │              │            │                   │
│  ─────┼─────────────┼────────────┼──────────────┼────────────┼──── 接口边界 ──  │
│       │             │            │              │            │                   │
│  ┌────▼─────────────▼────┐ ┌────▼─────┐ ┌─────▼─────┐ ┌────▼─────┐            │
│  │  算法引擎系统         │ │IImageR.  │ │ GPIO/PLC  │ │ Template │            │
│  │  (IAlgorithmEngine)   │ │ 实现     │ │ 控制器    │ │ Hierarchy│            │
│  └────┬──────┬───────┬───┘ └──────────┘ └───────────┘ └──────────┘            │
│       │      │       │                                                          │
│  ┌────▼──┐┌──▼───┐┌──▼──┐                      ┌────────────┐                  │
│  │OpenCV ││ ONNX ││ VM  │                      │ 3D 子系统  │                  │
│  │Engine ││Engine││Eng. │                      │(IThreeDSvc)│                  │
│  └───────┘└──────┘└─────┘                      └──────┬─────┘                  │
│                                                        │ Named Pipe IPC        │
└────────────────────────────────────────────────────────┼────────────────────────┘
                                                         │
                                              ┌──────────▼──────────┐
                                              │  Slide.ThreeD.Host  │
                                              │  (独立 x64 进程)    │
                                              └─────────────────────┘

─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─  程序集加载边界（动态加载）─ ─ ─ ─ ─ ─ ─ ─ ─ ─

┌───────────────────────────────────────────────────────────┐
│              平台层 (src/)  —  netstandard2.0              │
│  ┌─────────────────────────┐  ┌────────────────────────┐  │
│  │ Slide.Platform.          │  │ Slide.Platform.         │  │
│  │ Abstractions             │  │ Runtime                 │  │
│  │ (IAlgorithmPlugin,       │◄─┤ (PluginLoader,          │  │
│  │  IAlgorithmSession)      │  │  PluginRegistry)        │  │
│  └────────────▲─────────────┘  └────────────────────────┘  │
│               │                                             │
└───────────────┼─────────────────────────────────────────────┘
                │ 编译时引用
┌───────────────┼─────────────────────────────────────────────┐
│  动态插件 (plugins/)  —  netstandard2.0                     │
│  ┌────────────┴───────────┐  ┌───────────────────────────┐  │
│  │ Slide.Algorithm.OpenCv │  │ Slide.Algorithm.Onnx      │  │
│  │ (实现 IAlgorithmPlugin)│  │ (实现 IAlgorithmPlugin)   │  │
│  └────────────────────────┘  └───────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 功能块详解

### 1. UI 层

| 组件 | 文件 | 职责 |
|------|------|------|
| **Page1 主检测页** | `UI/Page1.xaml.cs` | 检测流程编排、用户交互入口 |
| **TemplateConfigPage** | `UI/TemplateConfigPage.xaml.cs` | 模板参数配置向导 |
| **ImageInspectionViewer** | `UI/Controls/ImageInspectionViewer.xaml.cs` | 图像预览控件（最多 10 个实例） |
| **TrayGridControl** | `UI/Controls/TrayGridControl.xaml.cs` | Tray 检测网格 |
| **SmartAnalysisWidget** | `UI/Controls/SmartAnalysisWidget.xaml.cs` | 分析结果可视化 |
| **SmartInputCard** | `UI/Controls/SmartInputCard.xaml.cs` | 参数输入控件 |

**对外暴露的事件**：`AlgorithmResultProduced`（检测结果事件流）

---

### 2. 管线编排（Image Pipeline）

| 组件 | 文件 | 职责 |
|------|------|------|
| **ImagePipeline** | `UI/Page1.ImagePipeline.cs` | 图像管线：加载 → 构建输入 → 执行 → 产出结果 |
| **ImageSourceNaming** | `UI/ImageSourceNaming.cs` | 动态图像源名称解析 |
| **CICD 管线** | `UI/Page1.Cicd.cs` | 图片集批量制作/测试/对比 |
| **单片测试** | `UI/Page1.SingleSampleDynamicStatic.cs` | 单片动态/静态测试 |

**接口依赖**：
- → `IAlgorithmEngine`（向算法引擎系统请求检测）
- → `IImageRenderer`（将结果图像推送到渲染系统）
- → `ImageSourceNaming`（解析当前 Profile 的图像源定义）

---

### 3. 算法引擎系统

```
                        ┌───────────────────────────┐
                        │  AlgorithmEngineRegistry   │
                        │  ResolveEngine(engineId)   │
                        └─────┬──────┬──────┬───────┘
                              │      │      │
              ┌───────────────▼─┐ ┌──▼────┐ ┌──▼──────────────────┐
              │ OpenCvAlgorithm │ │ Onnx  │ │ OpenCvOnnxAlgorithm │
              │ Engine          │ │ Alg.  │ │ Engine (组合管线)    │
              │ ID: "OpenCV"    │ │ Eng.  │ │ ID: "OpenCV+ONNX"   │
              └─────────────────┘ │"ONNX" │ │ ← 默认引擎          │
                                  └───────┘ └─────────────────────┘
```

**耦合接口**：`IAlgorithmEngine`（定义于 `Slide.Algorithm.Contracts`）

```csharp
interface IAlgorithmEngine {
    string EngineId { get; }
    bool IsAvailable { get; }
    Task<AlgorithmResult> ExecuteAsync(AlgorithmInput input, CancellationToken ct);
}
```

**数据契约**：
- `AlgorithmInput`：模板名、Lot 号、多图像路径、参数字典
- `AlgorithmResult`：执行状态、测量值列表 (`AlgorithmMeasurement`)、渲染图像、调试信息

---

### 4. 渲染系统

```
    ┌─────────────────────────┐
    │  ImageRendererManager    │
    │  ResolveRenderer()       │
    └──────────┬──────────────┘
               │
    ┌──────────▼──────────────┐
    │  FileImageRenderer       │ ──→ ImageRendererContext
    │  (IImageRenderer 实现)   │      (持有 10 个 ImageInspectionViewer)
    └─────────────────────────┘
```

**耦合接口**：`IImageRenderer`（定义于 `Rendering/ImageRendererContracts.cs`）

```csharp
interface IImageRenderer {
    string RendererId { get; }
    void Bind(ImageRendererContext context);
    void DisplayImageGroup(ImageGroupSet group);
    void Clear();
}
```

---

### 5. 硬件抽象

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ GenericCamera     │     │ SMTGPIOController │     │ PLCSerialController  │
│ Manager           │     │ (GPIO 控制)       │     │ (PLC 串口通信)       │
│ ・OptCameraService│     │ ・IOManager       │     │                      │
│ ・CameraCatalog   │     │ ・GPIOConfig      │     │                      │
└──────────────────┘     └──────────────────┘     └──────────────────────┘
       Hardware/                 SMTGPIO/                  SMTGPIO/
```

**说明**：三个子系统互不依赖，各自封装硬件通信细节，由 UI 层直接调用。

---

### 6. 3D 集成子系统

```
  宿主进程内                                  独立进程 (x64)
┌──────────────────────┐   Named Pipe    ┌─────────────────────┐
│ IThreeDService       │ ◄──────────────►│ Slide.ThreeD.Host   │
│ ├ NamedPipeThreeD-   │   IPC 通信      │ (Keyence/LjDev SDK) │
│ │ Service (客户端)   │                 └─────────────────────┘
│ ├ NullThreeDService  │
│ │ (空实现，3D 禁用时)│
│ └ ThreeDSettings     │
└──────────────────────┘
```

**耦合接口**：`IThreeDService`（定义于 `ThreeD/ThreeDService/IThreeDService.cs`）

```csharp
interface IThreeDService : IDisposable {
    ThreeDStatus GetStatus(int timeoutMs);
    ThreeDExecuteResult ExecuteLocalImages(request, int timeoutMs);
    bool SaveAfterJudgement(request, out errorMessage, int timeoutMs);
}
```

**共享契约**：`Slide.ThreeD.Contracts`（`ThreeDIpcRequest` / `ThreeDIpcResponse`，DataContract 序列化）

**进程隔离原因**：Keyence/LjDev 程序集仅支持 x64，避免与主进程 AnyCPU 冲突。

---

### 7. 配置系统

```
┌───────────────────────────────────────────────────────────────────┐
│                     配置系统（全局静态访问）                         │
│                                                                    │
│  ┌──────────────────────┐  ┌───────────────────────────────────┐  │
│  │ TemplateHierarchy-    │  │ AlgorithmEngineSettings           │  │
│  │ Config (单例)         │  │ ← Config/AlgorithmEngine.json     │  │
│  │ ← Config/Template-    │  └───────────────────────────────────┘  │
│  │   Hierarchy.json      │                                         │
│  │ ・ResolveProfile()    │  ┌───────────────────────────────────┐  │
│  │ ・ImageSourceDef[]    │  │ RendererSettingsManager            │  │
│  │ ・LegacyMappings     │  │ ← Config/Renderer.json             │  │
│  └──────────────────────┘  └───────────────────────────────────┘  │
│                                                                    │
│  ┌──────────────────────┐  ┌───────────────────────────────────┐  │
│  │ ParameterConfigs      │  │ SystemBrandingManager              │  │
│  │ ← Config/Parameter-   │  │ ← Config/SystemBranding.json      │  │
│  │   Configs.json        │  └───────────────────────────────────┘  │
│  └──────────────────────┘                                          │
│                                                                    │
│  ┌──────────────────────┐  ┌───────────────────────────────────┐  │
│  │ DeviceManager         │  │ RealTimeDataExportConfig           │  │
│  │ (设备管理)            │  │ ← Config/RealTimeDataExport-       │  │
│  └──────────────────────┘  │   Config.json                      │  │
│                             └───────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────┘
```

---

### 8. 平台层（可复用 SDK）

```
┌─────────────────────────────────────────────────────────┐
│  Slide.Platform.Runtime (netstandard2.0)                 │
│  ┌────────────────┐  ┌────────────────────────────────┐ │
│  │ PluginLoader    │  │ PluginRegistry                 │ │
│  │ .LoadFrom-      │  │ (已加载插件的注册表)            │ │
│  │  Directory()    │  │                                │ │
│  └───────┬────────┘  └────────────────────────────────┘ │
│          │ 依赖                                          │
│  ┌───────▼────────────────────────────────────────────┐  │
│  │ Slide.Platform.Abstractions (netstandard2.0)        │  │
│  │  ・IAlgorithmPlugin { Descriptor; CreateSession() } │  │
│  │  ・IAlgorithmSession { Run(AlgorithmInput) }        │  │
│  │  ・AlgorithmInput / AlgorithmResult / Descriptor    │  │
│  └────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## 接口耦合边界汇总

| # | 接口 / 契约 | 定义位置 | 上游（调用方） | 下游（实现方） |
|---|------------|---------|-------------|-------------|
| 1 | `IAlgorithmEngine` | `Slide.Algorithm.Contracts` | Page1.ImagePipeline, AlgorithmEngineRegistry | OpenCvEngine, OnnxEngine, OpenCvOnnxEngine |
| 2 | `IImageRenderer` | `Rendering/ImageRendererContracts.cs` | Page1.ImagePipeline, ImageRendererManager | FileImageRenderer |
| 3 | `IThreeDService` | `ThreeD/ThreeDService/IThreeDService.cs` | Page1.ImagePipeline | NamedPipeThreeDService, NullThreeDService |
| 4 | `IAlgorithmPlugin` | `Slide.Platform.Abstractions` | PluginLoader (Runtime) | 外部插件 DLL (OpenCv, Onnx) |
| 5 | `IAlgorithmSession` | `Slide.Platform.Abstractions` | 宿主调用 `plugin.CreateSession()` | 插件内 Session 实现 |
| 6 | `ThreeDIpcRequest/Response` | `Slide.ThreeD.Contracts` | NamedPipeThreeDService | Slide.ThreeD.Host (独立进程) |
| 7 | `AlgorithmResultProduced` (事件) | `Page1.xaml.cs` | 检测管线产出 | UI 控件订阅更新 |
| 8 | `TemplateProfileDefinition` (数据契约) | `UI/Models/TemplateHierarchyConfig.cs` | ImageSourceNaming, Page1 | TemplateHierarchy.json |

---

## 程序集引用关系（DAG）

```
PlatformHost.Wpf ─────────┬──→ Slide.Platform.Runtime
(WpfApp2 / Slide.exe)     │         └──→ Slide.Platform.Abstractions
                           │
                           ├──→ Slide.Algorithm.Contracts
                           │
                           ├──→ Slide.Algorithm.OpenCV ──→ Slide.Algorithm.Contracts
                           ├──→ Slide.Algorithm.ONNX   ──→ Slide.Algorithm.Contracts
                           ├──→ Slide.Algorithm.VM     ──→ Slide.Algorithm.Contracts
                           │
                           └──→ Slide.ThreeD.Contracts

Slide.ThreeD.Host (独立进程) ──→ Slide.ThreeD.Contracts

Slide.Algorithm.OpenCv (插件) ──→ Slide.Platform.Abstractions
Slide.Algorithm.Onnx   (插件) ──→ Slide.Platform.Abstractions
Slide.Algorithm.Sample (示例) ──→ Slide.Platform.Abstractions

Slide.Platform.SampleHost ──→ Slide.Platform.Runtime
                               └──→ Slide.Platform.Abstractions
```

**关键观察**：
- 宿主同时依赖 **平台层** (`Runtime`) 和 **宿主内引擎层** (`Contracts`)，两套算法体系并存
- 插件仅依赖 `Abstractions`，与宿主完全解耦
- 3D 子系统通过进程隔离 + IPC 实现最强解耦
- 三个引擎实现 (`OpenCV`, `ONNX`, `VM`) 仅依赖 `Contracts`，互不引用

---

## 数据流全景

```
用户操作
   │
   ▼
┌──────────────┐   SelectImageFilesAsync()   ┌───────────────────┐
│   Page1 UI   │ ──────────────────────────►  │ ImageSourceNaming  │
│              │ ◄──────────────────────────  │ (解析图像源定义)   │
│              │    List<ImageGroupSet>        └───────────────────┘
│              │                                       ▲
│              │                                       │ 读取 Profile
│              │                              ┌────────┴──────────┐
│              │                              │ TemplateHierarchy- │
│              │                              │ Config (JSON 配置) │
│              │                              └───────────────────┘
│              │
│   Page1.     │   BuildAlgorithmInput()
│   Image-     │ ─────────────────────┐
│   Pipeline   │                      ▼
│              │          ┌───────────────────────┐
│              │          │ AlgorithmEngineRegistry│
│              │          │ ResolveEngine()        │
│              │          └─────────┬─────────────┘
│              │                    │
│              │                    ▼
│              │          ┌───────────────────────┐
│              │          │ IAlgorithmEngine       │
│              │          │ .ExecuteAsync()        │
│              │          └─────────┬─────────────┘
│              │                    │
│              │    ┌───────────────┼────────────────┐
│              │    │               │                 │   (并行, 可选)
│              │    ▼               ▼                 ▼
│              │  OpenCV          ONNX          IThreeDService
│              │  Engine          Engine        .ExecuteLocalImages()
│              │    │               │                 │
│              │    └───────┬───────┘                 │
│              │            │                         │
│              │            ▼                         │
│              │  ┌─────────────────┐                 │
│              │  │ AlgorithmResult │ ◄───────────────┘
│              │  └────────┬────────┘
│              │           │
│              │  AlgorithmResultProduced (事件)
│              │           │
│   ┌──────────┼───────────┼──────────────────────┐
│   │          ▼           ▼                      │
│   │  ┌────────────┐ ┌──────────────────────┐   │
│   │  │IImageRender│ │DetectionDataStorage  │   │
│   │  │.Display()  │ │(持久化检测数据)       │   │
│   │  └────────────┘ └──────────────────────┘   │
│   │      渲染          存储                     │
│   └─────────────────────────────────────────────┘
└──────────────┘
```

---

## 模块-责任-依赖-入口矩阵

| 模块 | 主要责任 | 关键依赖 | 入口文件 |
|------|---------|---------|---------|
| 入口/生命周期层 | 单实例、异常处理、启动诊断 | LogManager, IOManager | `App.xaml.cs` |
| 展示与导航层 | 主窗体、页面切换 | Page1, ConfigPage | `MainWindow.xaml.cs` |
| 业务编排层 | 2D/3D 检测流程编排 | AlgorithmEngineRegistry, IThreeDService | `Page1.ImagePipeline.cs` |
| 应用服务层 | 日志、模板、设备管理 | TemplateHierarchyConfig, DeviceManager | `UI/Models/*.cs` |
| 算法合约块 | 引擎接口规范 | IAlgorithmEngine, AlgorithmResult | `Slide.Algorithm.Contracts/` |
| 算法实现块 | OpenCV/ONNX/组合引擎 | 算法合约块 | `AlgorithmEngineRegistry.cs` |
| 3D 合约块 | IPC 数据模型 | ThreeDIpcRequest/Response | `Slide.ThreeD.Contracts/` |
| 3D 接口块 | 服务抽象 | IThreeDService | `ThreeD/ThreeDService/` |
| 3D 实现块 | NamedPipe 客户端 | 3D 合约块 | `NamedPipeThreeDService.cs` |
| 3D 外部进程块 | Keyence/LjDev 硬件交互 | 3D 合约块 | `Slide.ThreeD.Host/` |
| 设备与 IO 块 | GPIO/PLC 硬件通信 | IOManager, SMTGPIOController | `SMTGPIO/` |
| 相机块 | OPT 相机采集 | OptCameraService, SciCamera.Net | `Hardware/` |
| 渲染接口块 | 图像显示抽象 | IImageRenderer | `Rendering/ImageRendererContracts.cs` |
| 渲染实现块 | 文件渲染器 | 渲染接口块 | `Rendering/FileImageRenderer.cs` |
| 平台 SDK 层 | 插件加载基础设施 | IAlgorithmPlugin, PluginLoader | `src/` |
| 动态插件层 | 外部算法扩展 | 平台 SDK 层 | `plugins/` |
