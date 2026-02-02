
本项目图像输入数量的修改方式（默认 1 张，支持通过配置动态改为 2 张或更多）

目标
- 默认只输入 1 张图像。
- 通过配置 JSON 改动即可切换为 2 张或更多，系统 UI/流程自动适配。

改动入口（首选）
1) 配置文件：`PlatformHost.Wpf/Config/TemplateHierarchy.json`
   - 每个 Profile 的 `ImageSources` 列表决定输入图数量与名称。
   - 示例（1 张）：
     - ImageSources: [{ Id: "Image1", DisplayName: "图像1" }]
   - 示例（2 张）：
     - ImageSources:
       - { Id: "Image1", DisplayName: "图像1" }
       - { Id: "Image2", DisplayName: "图像2" }
   - 示例（3 张）：
     - ImageSources:
       - { Id: "Image1", DisplayName: "图像1" }
       - { Id: "Image2", DisplayName: "图像2" }
       - { Id: "Image3", DisplayName: "图像3" }

2) 默认回退配置（无 JSON 或 JSON 缺失时生效）
   - `PlatformHost.Wpf/UI/Models/TemplateHierarchyConfig.cs`
   - `CreateDefaultImageSources()` 返回默认图像源列表。
   - 需要保持与 JSON 一致（目前默认 1 张）。

核心适配逻辑（了解即可）
- `PlatformHost.Wpf/UI/ImageSourceNaming.cs`
  - `GetActiveSourceCount()` 读取当前 Profile 的图像数量（最少 1）。
  - `GetFolderCandidates(index)` 会同时兼容历史目录名：
    - index=1 → "图像源2_1"
    - index=2 → "图像源2_2"
  - 也会加入 `图像源{n}`、`Image{n}` 的兜底名称。

- 动态适配入口（已改为按配置数量运行）：
  - `PlatformHost.Wpf/UI/Page1.xaml.cs`
    - 选图/匹配/存图/验机/导出流程都以 `GetRequired2DSourceCount()` 为准。
  - `PlatformHost.Wpf/UI/TemplateConfigPage.xaml.cs`
    - 自动匹配、提示文案、模板拷贝结构都按配置数量生成。
  - `ImageGroupSet.Has2DImages / IsValid` 已按“配置数量”判断。

目录约定与兼容
- 推荐目录名：与 `DisplayName` 保持一致。
- 旧目录名仍可识别：`图像源2_1`、`图像源2_2`。
- 3D 目录固定为 `3D`。

常见操作步骤（改为 2 张 / 3 张）
1) 修改 `PlatformHost.Wpf/Config/TemplateHierarchy.json` 的 `ImageSources` 列表数量。
2) 保持 `TemplateHierarchyConfig.CreateDefaultImageSources()` 同步（如需默认改动）。
3) 确保模板/样本目录存在对应数量的图像源子目录（名称与 DisplayName 或旧兼容名一致）。
4) 重新编译验证。

编译命令（Release）
- 常规：
  - `D:\CodingSys\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe .\GlueInspect.Platform.sln /p:Configuration=Release /m`
- 若遇到 `GlueInspect.pdb` 被占用（锁定写入）：
  - 关闭占用进程或删除 `PlatformHost.Wpf/obj/Release/GlueInspect.pdb` 后重编。
  - 或用无 PDB 输出（可过 CI/本地验证）：
    - `...\MSBuild.exe .\GlueInspect.Platform.sln /p:Configuration=Release /p:DebugType=None /p:DebugSymbols=false /m`

注意事项
- 修改图像源数量后，建议同时检查模板配置页与图片测试模式是否能正确显示/匹配。
- 只需要改 JSON 即可快速切换；代码已适配动态数量，不再需要硬编码 3 张。
