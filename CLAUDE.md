# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目愿景

这是一个**通用视觉检测平台**，正在从原有的点胶检测项目演化而来。平台已建立稳定的模板配置、图像处理管线、数据处理等可复用流程。

**目标**：完成通用平台后，构建 skill，使其他工程师能够借助 AI 快速搭建针对特定检测物件的视觉检测系统。

## 构建命令

```bash
# 构建整个解决方案
dotnet build Slide.Platform.sln

# 构建 WPF 宿主应用（Release 配置）
msbuild PlatformHost.Wpf\PlatformHost.Wpf.csproj /p:Configuration=Release

# 运行示例宿主加载插件
dotnet run --project samples/Slide.Platform.SampleHost -- ./plugins <图像路径> [算法ID]
```

## 架构概述

### 双层架构

**平台层** (`src/`):
- `Slide.Platform.Abstractions` (netstandard2.0): 插件接口 (`IAlgorithmPlugin`, `IAlgorithmSession`) 和数据模型
- `Slide.Platform.Runtime` (netstandard2.0): 插件加载器 (`PluginLoader.LoadFromDirectory`) 和注册表

**宿主层** (`PlatformHost.Wpf/`):
- 基于 .NET Framework 4.8 的 WPF 应用
- 命名空间: `WpfApp2`（历史遗留命名空间）

### Page1 核心模块

`Page1.xaml.cs` 是主检测界面，采用 partial class 拆分为多个功能模块：

| 文件 | 职责 |
|------|------|
| `Page1.xaml.cs` | 主 UI 逻辑、检测流程编排、VM 回调处理 |
| `Page1.ImagePipeline.cs` | 图像管线：`ImageGroupSet` 加载、算法输入构建、检测执行 |
| `Page1.Cicd.cs` | CICD 图片集制作/测试/对比功能 |
| `Page1.SingleSampleDynamicStatic.cs` | 单片动态/静态测试集功能 |

### 图像源命名系统

`ImageSourceNaming` (`UI/ImageSourceNaming.cs`) 提供动态图像源名称解析：
- `GetActiveImageSources()`: 获取当前模板配置的图像源列表
- `GetDisplayNames()`: 获取图像源显示名称
- `GetFolderCandidates(index)`: 获取图像源对应的文件夹候选名
- 图像源数量和名称由 `TemplateHierarchy.json` 中的 Profile 配置决定

### 算法引擎系统

位于 `PlatformHost.Wpf/Algorithms/`:

- `IAlgorithmEngine` 接口定义在 `Slide.Algorithm.Contracts`
- 引擎 ID: `VM`, `OpenCV`, `ONNX`, `OpenCV+ONNX`
- `AlgorithmEngineRegistry` 管理引擎生命周期和解析
- 默认引擎: `OpenCV+ONNX`（组合管线：经典 CV + 深度学习）

### 图像渲染系统

位于 `PlatformHost.Wpf/Rendering/`:

- `IImageRenderer` 接口用于显示抽象
- `ImageRendererContext` 支持最多 10 个 `ImageInspectionViewer` 预览窗口
- `ImageRendererManager.ResolveRenderer()` 返回 `FileImageRenderer`

### 模板配置系统

`TemplateHierarchyConfig` (`UI/Models/TemplateHierarchyConfig.cs`) 管理模板配置：
- 配置文件: `Config/TemplateHierarchy.json`
- `TemplateProfileDefinition`: 定义模板配置（步骤序列、图像源、全局变量）
- `ImageSourceDefinition`: 定义图像源（Id + DisplayName）
- `ResolveProfile(profileId)`: 解析模板配置
- 支持 `LegacyMappings` 兼容旧版 SampleType/CoatingType 映射

### 3D 集成

位于 `PlatformHost.Wpf/ThreeD/`:

- `IThreeDService` 抽象 3D 操作（主进程不直接引用 Keyence/LjDev 程序集）
- `NamedPipeThreeDService` 通过 IPC 与独立 3D 宿主进程通信
- `Slide.ThreeD.Host` 作为独立进程运行，隔离 3D 硬件依赖

### 硬件抽象

- `SMTGPIO/`: GPIO 和 PLC 串口通信 (`SMTGPIOController`, `PLCSerialController`)
- `Hardware/`: 通用相机管理 (`GenericCameraManager`)

### 配置文件

位于 `PlatformHost.Wpf/Config/`:

| 文件 | 用途 |
|------|------|
| `AlgorithmEngine.json` | 首选算法引擎 |
| `TemplateHierarchy.json` | 模板配置（Profile、步骤序列、图像源） |
| `ParameterConfigs.json` | 参数显示配置 |
| `Renderer.json` | 图像渲染器设置 |

### 插件开发

1. 引用 `Slide.Platform.Abstractions`
2. 实现 `IAlgorithmPlugin`（描述符 + 会话工厂）
3. 实现 `IAlgorithmSession`（输入 -> 结果执行）
4. 将编译后的 DLL 放入宿主的 `plugins/` 目录

示例插件位于 `plugins/`:
- `Slide.Algorithm.OpenCv`: 基于 OpenCV 的检测
- `Slide.Algorithm.Onnx`: ONNX 模型推理

## 关键模式

- 算法引擎通过 `AlgorithmEngineRegistry.ResolveEngine(engineId)` 解析
- 检测结果通过 `Page1` 上的 `AlgorithmResultProduced` 事件流转
- 图像源配置通过 `TemplateHierarchy.json` 中 Profile 的 `ImageSources` 定义
- `ImageGroupSet` 封装多图像源组，支持动态数量的图像源
- 3D 操作隔离在独立进程中以避免程序集冲突

## 数据流

1. 用户选择图像 → `SelectImageFilesAsync()` 构建 `ImageGroupSet`
2. `ImageGroupSet` 根据 `ImageSourceNaming` 匹配多图像源文件夹
3. `BuildAlgorithmInput()` 将 `ImageGroupSet` 转换为 `AlgorithmInput`
4. `AlgorithmEngineRegistry.ResolveEngine()` 获取引擎实例
5. `ExecuteAlgorithmPipelineForImageGroup()` 执行检测
6. 结果通过 `AlgorithmResultProduced` 事件通知 UI
