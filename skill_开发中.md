本项目图像输入数量的修改方式（默认 1 张，最大 10 张）

目标
- 默认只输入 1 张图像。
- 通过配置 JSON 改动即可切换为 2 张或更多（最多 10），系统 UI/流程自动适配。

改动入口（首选）
1) 配置文件：`PlatformHost.Wpf/Config/TemplateHierarchy.json`
   - 每个 Profile 的 `ImageSources` 列表决定输入图数量与名称。
   - `Id` / `DisplayName` 可任意命名，`_1`、`_2` 只是普通名称，不做特殊处理。
   - 示例（1 张）：
     - ImageSources: [{ Id: "Image1", DisplayName: "图像1" }]
   - 示例（2 张）：
     - ImageSources:
       - { Id: "Image1", DisplayName: "图像1" }
       - { Id: "Image2", DisplayName: "图像2" }
   - 示例（4 张）：
     - ImageSources:
       - { Id: "Image1", DisplayName: "图像1" }
       - { Id: "Image2", DisplayName: "图像2" }
       - { Id: "Image3", DisplayName: "图像3" }
       - { Id: "Image4", DisplayName: "图像4" }

2) 默认回退配置（无 JSON 或 JSON 缺失时生效）
   - `PlatformHost.Wpf/UI/Models/TemplateHierarchyConfig.cs`
   - `CreateDefaultImageSources()` 返回默认图像源列表。
   - 需要保持与 JSON 一致（目前默认 1 张）。

核心适配逻辑（了解即可）
- `PlatformHost.Wpf/UI/ImageSourceNaming.cs`
  - `GetActiveSourceCount()` 读取当前 Profile 的图像数量（最少 1，最多 10）。
  - `GetFolderCandidates(index)` 只使用 DisplayName / Id + 兜底名称：
    - `图像源{n}`、`Image{n}`。
  - 不再对 `_1` / `_2` 做任何特殊处理。

- `PlatformHost.Wpf/UI/Page1.xaml.cs`
  - `GetRequired2DSourceCount()` 会将数量限制为 10。
  - `BuildAlgorithmInput()` 以 `Source1..SourceN` 写入算法输入。
  - `ImageGroupSet` 内部支持 10 路路径（`GetPath/SetSource`）。

- `PlatformHost.Wpf/UI/TemplateConfigPage.xaml.cs`
  - 图片预览布局：
    - 1 张：`SingleImageContainer` 全屏占用。
    - 2+ 张：`MultiImageContainer` 动态行高；4 张即 2x2。
  - 图片标题颜色为绿色。
  - 图像路径参数保存键：`图像源{n}路径`（n 从 1 开始）。
  - 兼容读取旧键：`图像源2_1路径` / `图像源2_2路径`（仅回退，不再当特殊语义）。
  - 点击“执行”会刷新 DataGrid 并更新预览。

渲染图绑定（当前实现）
- `TemplateConfigPage` 会尝试读取 `Page1.LastAlgorithmResult.DebugInfo`：
  - ImageSelection -> `Result.Render.Input`
  - 预处理(DemoSetup) -> `Result.Render.Preprocess`
  - 边缘提取(DemoCalculation) -> `Result.Render.Edge`
  - 测量与判定(DemoSummary) -> `Result.Render.Composite`
- 扩展其他步骤：在 `TemplateConfigPage.GetRenderKeyCandidates` 里新增映射。

目录约定与兼容
- 推荐目录名：与 `DisplayName` 保持一致。
- 兜底名称：`图像源{n}` / `Image{n}`。
- 3D 目录固定为 `3D`。

常见操作步骤（改为 2 张 / 4 张 / 10 张）
1) 修改 `PlatformHost.Wpf/Config/TemplateHierarchy.json` 的 `ImageSources` 列表数量（最多 10）。
2) 需要默认改动时同步 `TemplateHierarchyConfig.CreateDefaultImageSources()`。
3) 确保模板/样本目录存在对应数量的图像源子目录（名称与 DisplayName 或兜底名一致）。
4) 重新编译验证。

编译命令（Release）
- 常规：
  - `D:\CodingSys\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe .\GlueInspect.Platform.sln /p:Configuration=Release /m`
- 若遇到 `GlueInspect.pdb` 被占用（锁定写入）：
  - 关闭占用进程或删除 `PlatformHost.Wpf/obj/Release/GlueInspect.pdb` 后重编。
  - 或用无 PDB 输出：
    - `...\MSBuild.exe .\GlueInspect.Platform.sln /p:Configuration=Release /p:DebugType=None /p:DebugSymbols=false /m`

注意事项
- 修改图像源数量后，建议检查模板配置页的预览布局与自动匹配提示。
- 如果模板中仍有旧键，会在读取时兼容；保存后统一使用 `图像源{n}路径`。
