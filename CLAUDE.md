# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目愿景

这是一个**通用视觉检测平台**，正在从原有的点胶检测项目演化而来。平台已建立稳定的模板配置、数据处理等可复用流程。

**目标**：完成通用平台后，构建 skill，使其他工程师能够借助 AI 快速搭建针对特定检测物件的视觉检测系统。

## 构建命令

```bash
# 构建整个解决方案
dotnet build GlueInspect.Platform.sln

# 构建 WPF 宿主应用（Release 配置）
msbuild PlatformHost.Wpf\PlatformHost.Wpf.csproj /p:Configuration=Release

# 运行示例宿主加载插件
dotnet run --project samples/GlueInspect.Platform.SampleHost -- ./plugins <图像路径> [算法ID]
```

## 架构概述

### 双层架构

**平台层** (`src/`):
- `GlueInspect.Platform.Abstractions` (netstandard2.0): 插件接口 (`IAlgorithmPlugin`, `IAlgorithmSession`) 和数据模型
- `GlueInspect.Platform.Runtime` (netstandard2.0): 插件加载器 (`PluginLoader.LoadFromDirectory`) 和注册表

**宿主层** (`PlatformHost.Wpf/`):
- 基于 .NET Framework 4.8 的 WPF 应用
- 主入口: `Page1.xaml.cs` - 主检测界面和检测流程编排
- 命名空间: `WpfApp2`（历史遗留命名空间）

### 算法引擎系统

位于 `PlatformHost.Wpf/Algorithms/`:

- `IAlgorithmEngine` 接口定义在 `GlueInspect.Algorithm.Contracts`
- 引擎 ID: `VM`, `OpenCV`, `ONNX`, `OpenCV+ONNX`
- `AlgorithmEngineRegistry` 管理引擎生命周期和解析
- 默认引擎: `OpenCV+ONNX`（组合管线：经典 CV + 深度学习）
- 配置文件: `Config/AlgorithmEngine.json`

### 图像渲染系统

位于 `PlatformHost.Wpf/Rendering/`:

- `IImageRenderer` 接口用于显示抽象
- `ImageRendererManager` 处理渲染器生命周期
- `ImageInspectionViewer` 控件用于带检测叠加层的图像显示

### 3D 集成

位于 `PlatformHost.Wpf/ThreeD/`:

- `IThreeDService` 抽象 3D 操作（主进程不直接引用 Keyence/LjDev 程序集）
- `NamedPipeThreeDService` 通过 IPC 与独立 3D 宿主进程通信
- `GlueInspect.ThreeD.Host` 作为独立进程运行，隔离 3D 硬件依赖

### 硬件抽象

- `SMTGPIO/`: GPIO 和 PLC 串口通信 (`SMTGPIOController`, `PLCSerialController`)
- `Hardware/`: 通用相机管理 (`GenericCameraManager`)

### 配置文件

位于 `PlatformHost.Wpf/Config/`:

- `AlgorithmEngine.json`: 首选算法引擎
- `TemplateHierarchy.json`: 模板配置（basic, standard, 3D），定义步骤序列
- `ParameterConfigs.json`: 参数显示配置
- `Renderer.json`: 图像渲染器设置

### 插件开发

1. 引用 `GlueInspect.Platform.Abstractions`
2. 实现 `IAlgorithmPlugin`（描述符 + 会话工厂）
3. 实现 `IAlgorithmSession`（输入 -> 结果执行）
4. 将编译后的 DLL 放入宿主的 `plugins/` 目录

示例插件位于 `plugins/`:
- `GlueInspect.Algorithm.OpenCv`: 基于 OpenCV 的检测
- `GlueInspect.Algorithm.Onnx`: ONNX 模型推理

## 关键模式

- 算法引擎通过 `AlgorithmEngineRegistry.ResolveEngine(engineId)` 解析
- 检测结果通过 `Page1` 上的 `AlgorithmResultProduced` 事件流转
- 模板配置使用 `TemplateHierarchy.json` 中定义的步骤序列
- 3D 操作隔离在独立进程中以避免程序集冲突
