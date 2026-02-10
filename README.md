# Slide Platform — 通用视觉检测平台

## 项目定位

工业视觉检测平台，支持 2D（OpenCV + ONNX）和 3D（Keyence）检测，配合 IO/PLC 硬件输出检测结果。平台正在从点胶检测专用系统演化为通用视觉检测平台。

---

## 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 宿主应用 | .NET Framework 4.8 / WPF | 主程序，传统 csproj 格式 |
| 平台 SDK | .NET Standard 2.0 | 统一算法接口、插件运行时，跨框架兼容 |
| 算法引擎 | OpenCV 4.x / ONNX Runtime | 经典 CV + 深度学习组合管线 |
| 3D 检测 | Keyence LJ-X 系列 | 独立进程，Named Pipe IPC |
| 硬件控制 | GPIO (SMT) / PLC (三菱串口) | 接口抽象，支持 Null 实现 |

---

## 项目结构总览

```
Platform/
│
├── Slide.Platform.sln                    # 解决方案文件
│
│   ┌─────────────────────────────────────────────────────────┐
│   │                    平台 SDK 层                          │
│   │              (netstandard2.0, 跨框架)                   │
├── src/ │                                                    │
│   ├── Slide.Platform.Abstractions/      # 统一算法接口层    │
│   │   ├── IAlgorithmPlugin.cs           #   插件入口接口    │
│   │   ├── IAlgorithmSession.cs          #   执行会话接口    │
│   │   ├── SimpleAlgorithmInput.cs       #   简单输入(插件用)│
│   │   ├── SimpleAlgorithmResult.cs      #   简单输出(插件用)│
│   │   ├── IAlgorithmEngine.cs           #   统一引擎接口    │
│   │   ├── AlgorithmInput.cs             #   丰富输入模型    │
│   │   ├── AlgorithmResult.cs            #   丰富输出模型    │
│   │   ├── AlgorithmEngineIds.cs         #   引擎 ID 常量    │
│   │   ├── AlgorithmEngineDescriptor.cs  #   引擎描述        │
│   │   ├── AlgorithmExecutionStatus.cs   #   执行状态枚举    │
│   │   ├── AlgorithmMeasurement.cs       #   测量数据        │
│   │   └── IAlgorithmEngineMetadata.cs   #   Schema 自描述   │
│   │                                                         │
│   └── Slide.Platform.Runtime/           # 插件运行时        │
│       ├── PluginLoader.cs               #   DLL 动态加载    │
│       ├── AlgorithmRegistry.cs          #   插件注册表      │
│       ├── EngineRegistry.cs             #   引擎注册表      │
│       ├── PluginEngineAdapter.cs        #   Plugin→Engine桥接│
│       └── Tray/                         #   托盘检测模块    │
│           ├── TrayComponent.cs          #     托盘编排      │
│           ├── TrayDataManager.cs        #     数据管理      │
│           └── ITrayRepository.cs        #     存储接口      │
│   └─────────────────────────────────────────────────────────┘
│
│   ┌─────────────────────────────────────────────────────────┐
│   │              宿主应用层 (.NET Framework 4.8)             │
│   │                 命名空间: WpfApp2                        │
├── PlatformHost.Wpf/ │                                       │
│   ├── App.xaml.cs                       # 应用入口          │
│   ├── MainWindow.xaml.cs                # 主窗口框架        │
│   │                                                         │
│   │   ── UI/ ──────────────── 用户界面 ──────────────────   │
│   │   │                                                     │
│   │   ├── Page1.xaml.cs                 # 主检测界面 ★      │
│   │   ├── Page1.Cicd.cs                 #   CICD 测试       │
│   │   ├── Page1.SingleSampleDynamicStatic.cs # 单片测试     │
│   │   ├── UnifiedDetectionManager.cs    # 检测编排器 ★      │
│   │   ├── TemplateConfigPage.xaml.cs    # 模板配置界面      │
│   │   ├── ConfigPage.xaml.cs            # 系统配置          │
│   │   ├── ImageSourceNaming.cs          # 图像源命名解析    │
│   │   ├── TrayDetectionWindow.xaml.cs   # 托盘检测窗口      │
│   │   │                                                     │
│   │   ├── Controls/                     # 自定义控件 (18)   │
│   │   │   ├── ImageInspectionViewer     #   图像预览控件    │
│   │   │   ├── TrayGridControl           #   托盘网格控件    │
│   │   │   ├── SmartAnalysis*            #   智能分析组件    │
│   │   │   └── SmartInput*               #   智能输入组件    │
│   │   │                                                     │
│   │   └── Models/                       # 业务模型 (28)     │
│   │       ├── TemplateHierarchyConfig   #   模板层级配置    │
│   │       ├── DeviceManagement          #   设备管理        │
│   │       ├── LogManager                #   日志管理        │
│   │       ├── SystemBrandingManager     #   系统品牌        │
│   │       └── DetectionDataStorage      #   检测数据存储    │
│   │                                                         │
│   │   ── Algorithms/ ──────── 算法引擎 ──────────────────   │
│   │   │                                                     │
│   │   ├── AlgorithmEngineRegistry.cs    # 引擎注册表 ★      │
│   │   ├── OpenCvOnnxAlgorithmEngine.cs  # 组合引擎          │
│   │   ├── Slide.Algorithm.Contracts/    # 空壳(已迁移)      │
│   │   ├── Slide.Algorithm.OpenCV/       # OpenCV 引擎       │
│   │   │   └── (实现 IAlgorithmEngineMetadata)               │
│   │   ├── Slide.Algorithm.ONNX/         # ONNX 引擎        │
│   │   └── Slide.Algorithm.VM/           # VM 引擎          │
│   │                                                         │
│   │   ── ThreeD/ ──────────── 3D 检测 ───────────────────   │
│   │   │                                                     │
│   │   ├── ThreeDService/                # 3D 服务抽象       │
│   │   │   ├── IThreeDService.cs         #   服务接口        │
│   │   │   ├── NamedPipeThreeDService    #   IPC 实现        │
│   │   │   ├── NullThreeDService         #   空实现          │
│   │   │   └── ThreeDSettings.cs         #   3D 配置         │
│   │   ├── Slide.ThreeD.Contracts/       # 3D 数据契约       │
│   │   └── Slide.ThreeD.Host/            # 3D 独立进程 ★     │
│   │                                                         │
│   │   ── SMTGPIO/ ─────────── IO/PLC 硬件 ───────────────   │
│   │   │                                                     │
│   │   ├── IIoController.cs              # IO 接口 ★         │
│   │   ├── IPlcController.cs             # PLC 接口 ★        │
│   │   ├── IoControllerAdapter.cs        #   适配 IOManager  │
│   │   ├── PlcControllerAdapter.cs       #   适配 PLC 单例   │
│   │   ├── NullIoController.cs           #   IO 空实现       │
│   │   ├── NullPlcController.cs          #   PLC 空实现      │
│   │   ├── HardwareControllerFactory.cs  #   工厂            │
│   │   ├── IOManager.cs                  #   IO 管理器       │
│   │   ├── PLCSerialController.cs        #   PLC 串口控制    │
│   │   └── SMTGPIOController.cs          #   GPIO 底层驱动   │
│   │                                                         │
│   │   ── Hardware/ ────────── 相机 ──────────────────────   │
│   │   │                                                     │
│   │   ├── GenericCameraManager.cs       # 通用相机管理      │
│   │   └── OptCameraService.cs           # OPT 相机服务      │
│   │                                                         │
│   │   ── Rendering/ ───────── 图像渲染 ──────────────────   │
│   │   │                                                     │
│   │   ├── IImageRenderer.cs             # 渲染器接口        │
│   │   ├── FileImageRenderer.cs          # 文件渲染实现      │
│   │   └── ImageRendererManager.cs       # 渲染器管理        │
│   │                                                         │
│   │   ── Config/ ──────────── 配置文件 ──────────────────   │
│   │   │                                                     │
│   │   ├── TemplateHierarchy.json        # 模板配置 ★        │
│   │   ├── AlgorithmEngine.json          # 算法引擎配置      │
│   │   ├── ParameterConfigs.json         # 参数显示配置      │
│   │   ├── Renderer.json                 # 渲染器配置        │
│   │   ├── CameraCatalog.json            # 相机目录          │
│   │   └── SystemBranding.json           # 系统品牌配置      │
│   └─────────────────────────────────────────────────────────┘
│
│   ┌─────────────────────────────────────────────────────────┐
│   │                    外部插件层                            │
├── plugins/ │                                                │
│   ├── Slide.Algorithm.OpenCv/           # OpenCV 插件       │
│   └── Slide.Algorithm.Onnx/             # ONNX 插件         │
│   └─────────────────────────────────────────────────────────┘
│
│   ┌─────────────────────────────────────────────────────────┐
│   │                    示例与测试                            │
├── samples/ │                                                │
│   ├── Slide.Platform.SampleHost/        # 示例宿主(控制台)  │
│   └── Slide.Algorithm.Sample/           # 示例算法插件      │
│                                                              │
├── tests/ │                                                  │
│   └── (单元测试)                                            │
│   └─────────────────────────────────────────────────────────┘
```

---

## 核心数据流

```
┌──────────┐    ┌──────────────┐    ┌───────────────────┐    ┌──────────┐
│ 用户选图  │───→│ ImageGroupSet │───→│ AlgorithmEngine   │───→│ 检测结果  │
│ 或相机触发│    │ (多图像源组)  │    │ (OpenCV+ONNX)     │    │          │
└──────────┘    └──────────────┘    └───────────────────┘    └────┬─────┘
                                                                  │
                      ┌───────────────────────────────────────────┤
                      │                                           │
                      ▼                                           ▼
              ┌───────────────┐                          ┌──────────────┐
              │ UnifiedDetection│    等待 2D+3D 都完成    │  3D 检测      │
              │ Manager (编排器)│◄────────────────────────│  (独立进程)   │
              └───────┬───────┘                          └──────────────┘
                      │
                      ▼
              ┌───────────────┐    ┌──────────────┐
              │ 统一判定       │───→│ IO/PLC 输出   │───→ OK/NG 信号
              │ (综合2D+3D)   │    │ (硬件接口)    │
              └───────────────┘    └──────────────┘
```

**流程说明：**

1. 用户选择图像 → `ImageSourceNaming` 匹配多图像源文件夹 → 构建 `ImageGroupSet`
2. `AlgorithmEngineRegistry.ResolveEngine()` 获取引擎 → 执行 2D 检测
3. 3D 检测在独立进程中并行执行（通过 Named Pipe IPC）
4. `UnifiedDetectionManager` 等待 2D 和 3D 都完成后执行统一判定
5. 通过 `IIoController` / `IPlcController` 输出 OK/NG 信号到硬件

---

## 模块依赖关系

```
                    ┌─────────────────────────┐
                    │     Page1 (UI 层)        │
                    │  只负责 UI 事件分发       │
                    └──┬────┬────┬────┬───┬───┘
                       │    │    │    │   │
          ┌────────────┘    │    │    │   └──────────────┐
          ▼                 ▼    │    ▼                   ▼
  ┌───────────────┐ ┌──────────┐│┌──────────────┐ ┌───────────┐
  │ UnifiedDetection│ │Algorithm ││ IThreeDService│ │IIoController│
  │ Manager        │ │Engine    ││              │ │IPlcController│
  │ (检测编排)     │ │Registry  ││              │ │(硬件接口)  │
  └───────────────┘ └──────────┘│└──────────────┘ └───────────┘
                                │
                    ┌───────────┘
                    ▼
            ┌──────────────┐
            │TemplateConfig │
            │(模板配置系统) │
            └──────────────┘

  ────────── 接口边界 ──────────────────────────────────────

  AlgorithmEngineRegistry ──→ IAlgorithmEngine (无 UI 依赖)
  IThreeDService          ──→ NamedPipeThreeDService | NullThreeDService
  IIoController           ──→ IoControllerAdapter    | NullIoController
  IPlcController          ──→ PlcControllerAdapter   | NullPlcController
```

**设计原则：**
- 算法引擎层 **不依赖** UI 层（已消除循环依赖）
- 硬件层通过接口抽象，支持 Null 实现（无硬件环境可运行）
- 3D 检测隔离在独立进程，避免 DLL 冲突

---

## 统一算法接口

平台使用统一的算法接口体系，所有类型定义在 `Slide.Platform.Abstractions` (netstandard2.0)：

### 两套接口共存

| 接口层 | 用途 | 输入/输出类型 |
|--------|------|---------------|
| **插件接口** (`IAlgorithmPlugin` + `IAlgorithmSession`) | 外部插件 DLL | `SimpleAlgorithmInput` → `SimpleAlgorithmResult` |
| **引擎接口** (`IAlgorithmEngine`) | 内置引擎 + 动态注册 | `AlgorithmInput` → `AlgorithmResult` |

- `PluginEngineAdapter` 可将插件桥接为引擎，实现统一调度
- 引擎可选实现 `IAlgorithmEngineMetadata` 自描述参数和输出 Schema

### Schema 自描述

实现 `IAlgorithmEngineMetadata` 的引擎可以声明：
- `GetParameterSchema()` — 参数名、类型、默认值、分组
- `GetOutputSchema()` — 输出名、单位、是否有上下限

目前 `OpenCvAlgorithmEngine` 已实现此接口。

---

## Page1 partial class 拆分

Page1 是主检测界面，按功能拆分为多个文件：

| 文件 | 行数 | 职责 |
|------|------|------|
| `Page1.xaml.cs` | ~12,000 | 主 UI 逻辑、检测流程、VM 回调、图像管线 |
| `Page1.Cicd.cs` | ~1,750 | CICD 图片集制作/测试/对比 |
| `Page1.SingleSampleDynamicStatic.cs` | ~190 | 单片动态/静态测试 |
| `UnifiedDetectionManager.cs` | ~580 | 检测编排：2D/3D 状态管理、统一判定 |

> **注意**: `Page1.ImagePipeline.cs` 文件存在但未包含在 csproj 中，其方法已在 `Page1.xaml.cs` 中定义。

---

## 配置系统

模板配置是系统核心，定义了检测流程：

```
Config/TemplateHierarchy.json
  └── Profiles[]                    # 模板配置列表
      ├── ProfileId                 # 模板 ID
      ├── Steps[]                   # 检测步骤序列
      ├── ImageSources[]            # 图像源定义
      │   ├── Id                    #   图像源 ID
      │   └── DisplayName           #   显示名称
      └── GlobalVariables           # 全局变量
```

---

## 构建

```bash
# 方式一：VS MSBuild（推荐，完整编译含 XAML）
"D:\CodingSys\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ^
  PlatformHost.Wpf\PlatformHost.Wpf.csproj /p:Configuration=Debug /t:Restore;Build

# 方式二：dotnet build（仅编译平台 SDK 层和 C# 代码，XAML 会报错）
dotnet build Slide.Platform.sln

# 方式三：直接用 Visual Studio 2022 打开 Slide.Platform.sln
```

> **注意：** 宿主项目是 .NET Framework 4.8 传统格式，新增 .cs 文件必须手动在 .csproj 中添加 `<Compile Include="..." />`。

---

## 插件开发

1. 引用 `Slide.Platform.Abstractions`
2. 实现 `IAlgorithmPlugin`（描述符 + 会话工厂）
3. 实现 `IAlgorithmSession`（`SimpleAlgorithmInput` → `SimpleAlgorithmResult`）
4. 编译后将 DLL 放入宿主 `plugins/` 目录

```csharp
public class MyPlugin : IAlgorithmPlugin
{
    public AlgorithmDescriptor Descriptor => new AlgorithmDescriptor(
        id: "my.algorithm",
        name: "My Algorithm",
        version: new Version(1, 0, 0));

    public IAlgorithmSession CreateSession() => new MySession();
}

public class MySession : IAlgorithmSession
{
    public SimpleAlgorithmResult Run(SimpleAlgorithmInput input)
    {
        return new SimpleAlgorithmResult { Success = true, Message = "OK" };
    }

    public void Dispose() { }
}
```

---

## 快速体验（示例宿主）

```bash
# 编译
dotnet build Slide.Platform.sln

# 运行示例
dotnet run --project samples/Slide.Platform.SampleHost -- ./plugins <图像路径> [算法ID]

# OpenCV 示例
dotnet run --project samples/Slide.Platform.SampleHost -- ./plugins samples/assets/test.pgm opencv.basic

# ONNX 示例
dotnet run --project samples/Slide.Platform.SampleHost -- ./plugins "" onnx.identity samples/assets/identity.onnx
```
