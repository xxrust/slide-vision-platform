相机配置方式说明

目标
- 硬件配置页的相机数量与名称由配置文件控制。
- 相机基础参数（厂商/型号/序列号/曝光/增益/触发等）保存到独立配置文件。

配置入口（首选）
1) 相机目录配置（数量与名称）
   - 文件：`PlatformHost.Wpf/Config/CameraCatalog.json`
   - 结构：
     {
       "Cameras": [
         { "Id": "Default", "Name": "默认相机" }
       ]
     }
   - 说明：
     - `Id` 用于相机参数配置的唯一标识，`Name` 用于 UI 显示。
     - 增删列表项即可改变相机数量与名称。

2) 相机参数配置保存位置
   - 文件：运行时写入 `Config/GenericCameraProfiles.json`
   - 说明：每个相机会保存厂商/型号/序列号/曝光/增益/触发/延时等参数。

UI 关联
- 硬件配置页（HardwareConfigPage）：
  - 按 `CameraCatalog.json` 渲染相机卡片列表；每页 2 台，超出可翻页。
- 相机参数配置页（CameraConfigPage）：
  - 标题与显示可按需调整；当前固定显示“图像源1/图像源2”。

常见操作步骤
1) 修改 `PlatformHost.Wpf/Config/CameraCatalog.json` 的 `Cameras` 列表。
2) 重新编译并运行主程序。
3) 在硬件配置页修改相机参数并保存，生成/更新 `GenericCameraProfiles.json`。
