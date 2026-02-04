# Slide Platform

目标：将“平台”与“算法”和“具体业务项目”彻底分离。平台只定义接口与加载机制，算法以插件形式接入，业务项目（如点胶检测）仅作为示例工程存在。

## 结构

```
Platform/
  Slide.Platform.sln
  src/
    Slide.Platform.Abstractions/   # 插件接口与数据模型
    Slide.Platform.Runtime/        # 插件加载器/注册表
  plugins/
    Slide.Algorithm.OpenCv/        # OpenCV 插件
    Slide.Algorithm.Onnx/          # ONNX 插件
  samples/
    Slide.Algorithm.Sample/        # 示例算法插件
    Slide.Platform.SampleHost/     # 示例宿主（控制台）
    assets/
      identity.onnx                      # ONNX 示例模型
      test.pgm                           # OpenCV 示例图片
```

## 核心接口

- `IAlgorithmPlugin`：算法插件入口（描述信息 + 创建会话）
- `IAlgorithmSession`：算法执行会话（输入 -> 格式化数值输出）
- `AlgorithmInput/AlgorithmResult`：平台统一输入与输出结构

## 快速体验（示例宿主）

1. 编译示例插件与宿主  
   在 `Platform/` 下执行：
   - `dotnet build Slide.Platform.sln`

2. 将示例插件 DLL 复制到宿主的 `plugins` 目录  
   例如：
   - `samples/Slide.Platform.SampleHost/bin/Debug/net8.0/plugins/`

3. 运行宿主并传入图像文件路径（或任意文件路径）  
   ```
   dotnet run --project samples/Slide.Platform.SampleHost -- ./plugins E:\path\to\file.bin
   ```

输出示例（格式化数值）：
```
Metrics:
  ByteCount: 12345.0000
  MeanByte: 127.1234
  SizeKB: 12.0560
  StdByte: 73.4567
```

## OpenCV 插件示例

```
dotnet run --project samples/Slide.Platform.SampleHost -- ./plugins samples/assets/test.pgm opencv.basic
```

## ONNX 插件示例

```
dotnet run --project samples/Slide.Platform.SampleHost -- ./plugins "" onnx.identity samples/assets/identity.onnx
```

## 关于点胶项目

- 点胶项目 **保持原样不改动**，作为“业务示例”存在。
- 未来如果要接入平台，只需要在点胶项目中引用 `Slide.Platform.Abstractions` 并以插件方式装载算法，无需直接依赖 VM 或任意具体算法框架。

## 后续扩展建议

- 为每种算法平台实现独立插件项目（VM / OpenCV / ONNX / 自研）
- 在业务项目侧仅通过插件协议调用算法，实现“项目-算法”零耦合
