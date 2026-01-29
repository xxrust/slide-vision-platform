# GlueInspect Platform 架构图

本文档记录通用算法平台与业务项目、算法插件的解耦架构。平台仅提供协议与加载机制，不依赖具体算法框架，业务项目只通过接口调用。

## 架构图（逻辑视图）

```
+-----------------------------------------------------------+
|                        业务项目层                         |
|  (示例：点胶检测、其它客户项目)                            |
|  - 仅依赖 Platform.Abstractions                            |
|  - 不直接依赖任何算法平台/VM/OpenCV/ONNX                   |
+------------------------------+----------------------------+
                               |
                               | 统一接口调用 (IAlgorithm*)
                               v
+------------------------------+----------------------------+
|                   平台核心（Platform）                    |
|  Abstractions:                                               |
|   - IAlgorithmPlugin / IAlgorithmSession                    |
|   - AlgorithmInput / AlgorithmResult                        |
|  Runtime:                                                   |
|   - PluginLoader (加载)                                     |
|   - AlgorithmRegistry (注册/发现)                           |
+------------------------------+----------------------------+
                               |
                               | 插件发现与加载 (目录/配置)
                               v
+------------------------------+----------------------------+
|                        算法插件层                          |
|  - VM 插件 (可选，需加密狗)                                 |
|  - OpenCV 插件                                               |
|  - ONNX 插件                                                 |
|  - 自研插件                                                  |
|  - 示例插件 (GlueInspect.Algorithm.Sample)                  |
+------------------------------+----------------------------+

+-----------------------------------------------------------+
|                         运行时数据流                       |
|  业务项目 -> AlgorithmInput -> 算法插件 -> AlgorithmResult |
|  (输出格式化数值 Metrics / Tags / Success / Message)        |
+-----------------------------------------------------------+
```

## 组件说明

- 业务项目层
  - 代表具体客户/产线项目（如点胶检测）。
  - 不持有任何算法平台的 DLL 依赖。
  - 仅通过 `IAlgorithmPlugin` / `IAlgorithmSession` 调用算法。

- Platform.Abstractions
  - 定义插件协议与输入/输出结构。
  - 输入：`AlgorithmInput`（图像路径/字节 + 参数）。
  - 输出：`AlgorithmResult`（格式化数值 Metrics、Tags、状态）。

- Platform.Runtime
  - 负责插件的加载、扫描与注册。
  - 默认基于目录扫描 DLL，并实例化实现 `IAlgorithmPlugin` 的类型。

- 算法插件层
  - 对接实际算法平台（VM/OpenCV/ONNX/自研等）。
  - 单独编译、单独部署，按需加载。

## 边界与约束

- 平台不允许依赖任何具体算法 SDK（如 VM）。
- 业务项目不允许直接引用算法平台 DLL。
- 算法平台变更不影响业务项目，只需替换插件 DLL。

## 示例工程

- 平台解决方案：`Platform/GlueInspect.Platform.sln`
- 示例插件：`Platform/samples/GlueInspect.Algorithm.Sample`
- 示例宿主：`Platform/samples/GlueInspect.Platform.SampleHost`

## 未来扩展

- 支持配置化插件发现（JSON/DB）。
- 支持插件版本治理与兼容性策略。
- 支持沙盒运行、隔离与进程化部署（可选）。
