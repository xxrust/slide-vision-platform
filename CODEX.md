你是运行在本仓库 Platform/ 目录下的自动化代理。
任务：执行“全局模板分级+去样品/涂布创建流程”的改造计划。
目标：
- 新增全局模板分级定义文件，并以 ProfileId 驱动模板创建/加载/步骤配置
- 创建模板流程仅保留“创建一个模板”，不再走样品/涂布两级选择
- 旧模板通过 LegacyMappings 做兼容
- 更新 skill_开发中.md，写入示例业务结构

涉及文件（如已完成请保持不改）：
- PlatformHost.Wpf/Config/TemplateHierarchy.json
- PlatformHost.Wpf/UI/Models/TemplateHierarchyConfig.cs
- PlatformHost.Wpf/UI/Models/Class1.cs (TemplateParameters 增加 ProfileId + LoadFromFile 兼容)
- PlatformHost.Wpf/UI/Models/ModuleRegistry.cs (GetStepConfigurations 改为 ProfileId 驱动)
- PlatformHost.Wpf/UI/TemplateConfigPage.xaml.cs (Profile 初始化、全局变量写入、去 TYPE/涂布数目)
- PlatformHost.Wpf/UI/ConfigPage.xaml(+.cs) (创建流程改为模板档案选择)
- PlatformHost.Wpf/MainWindow.xaml.cs (加载模板改为 ProfileId)
- PlatformHost.Wpf/UI/Page1.xaml.cs (算法输入与占位指标改为 ProfileId)
- PlatformHost.Wpf/Algorithms/Slide.Algorithm.Contracts/AlgorithmContracts.cs (新增 TemplateProfileId/Name)
- PlatformHost.Wpf/Algorithms/Slide.Algorithm.OpenCV/OpenCvAlgorithmEngine.cs (Demo 逻辑改为 profile-basic)
- PlatformHost.Wpf/UI/Ljd3DDetectionWindow.xaml.cs (去 MESA 默认路径)
- PlatformHost.Wpf/UI/Models/RealTimeDataExportConfig.cs + Config/RealTimeDataExportConfig.json (默认模板名去业务化)
- skill_开发中.md (补充 TemplateHierarchy.json 与示例业务说明)

要求：
- 如果已完成，保持不改并直接输出 <promise>COMPLETE</promise>。
- 只修改上述范围内文件。
- 完成后输出 <promise>COMPLETE</promise>。
