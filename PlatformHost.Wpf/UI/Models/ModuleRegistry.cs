using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Media;
using WpfApp2.Models;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 模块注册表 - 集中管理所有模块的配置
    /// 实现高内聚、低耦合：每个模块的所有配置都在一个定义中
    /// 添加新模块只需在此注册，无需修改多处代码
    /// </summary>
    public static class ModuleRegistry
    {
        #region 单位转换常量和方法

        /// <summary>
        /// 像元尺寸（微米/像素），默认4微米
        /// </summary>
        public static double PixelSize { get; set; } = 4.0;

        /// <summary>
        /// 将微米(um)转换为像素(pixel)
        /// </summary>
        public static double UmToPixel(double micrometers)
        {
            return micrometers / PixelSize;
        }

        /// <summary>
        /// 将毫米(mm)转换为像素(pixel)
        /// </summary>
        public static double MmToPixel(double millimeters)
        {
            double micrometers = millimeters * 1000.0;
            return UmToPixel(micrometers);
        }

        /// <summary>
        /// Boolean转数字的转换函数
        /// </summary>
        public static Func<string, string> BoolToNumber = value =>
            bool.TryParse(value, out bool result) && result ? "1" : "0";

        /// <summary>
        /// um转像素的转换函数
        /// </summary>
        public static Func<string, string> UmToPixelStr = value =>
            UmToPixel(double.Parse(value)).ToString("F2");

        /// <summary>
        /// mm转像素的转换函数
        /// </summary>
        public static Func<string, string> MmToPixelStr = value =>
            MmToPixel(double.Parse(value)).ToString("F2");

        #endregion

        #region 模块定义集合

        /// <summary>
        /// 所有已注册的模块定义
        /// </summary>
        private static readonly List<ModuleDefinition> _allModules = new List<ModuleDefinition>();

        /// <summary>
        /// 获取所有已注册的模块
        /// </summary>
        public static IReadOnlyList<ModuleDefinition> AllModules => _allModules.AsReadOnly();

        /// <summary>
        /// 根据StepType获取模块定义
        /// </summary>
        public static ModuleDefinition GetModule(StepType stepType)
        {
            return _allModules.FirstOrDefault(m => m.StepType == stepType);
        }

        /// <summary>
        /// 注册模块定义
        /// </summary>
        public static void RegisterModule(ModuleDefinition module)
        {
            // 检查是否已存在
            var existing = _allModules.FirstOrDefault(m => m.StepType == module.StepType);
            if (existing != null)
            {
                _allModules.Remove(existing);
            }
            _allModules.Add(module);
        }

        #endregion

        private static string GetDefaultSampleImagePath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "SampleImages", "demo.png");
        }

        #region 静态构造函数 - 注册所有默认模块

        static ModuleRegistry()
        {
            RegisterAllDefaultModules();
        }

        /// <summary>
        /// 注册所有默认模块
        /// </summary>
        private static void RegisterAllDefaultModules()
        {
            // ========== 图片选择 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.ImageSelection,
                DisplayName = "图片选择",
                ModuleName = "图片选择",
                ModulePath = "",
                ModuleType = ModuleType.None,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "图片路径", DefaultValue = GetDefaultSampleImagePath(), Type = ParamType.FilePath },
                    new ModuleParameter { Name = "自动匹配多图", DefaultValue = "true", Type = ParamType.Boolean }
                },
                Actions = new List<ModuleAction>
                {
                    new ModuleAction { Name = "浏览图片", Handler = null }
                },
                Labels = new List<string>
                {
                    "请选择任意一张图片，系统将自动匹配其余图像。"
                }
            });

            // ========== 预处理（示例） ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.DemoSetup,
                DisplayName = "预处理",
                ModuleName = "",
                ModulePath = "",
                ModuleType = ModuleType.None,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "灰度模式", DefaultValue = "true", Type = ParamType.Boolean, Group = "预处理" },
                    new ModuleParameter { Name = "高斯核尺寸", DefaultValue = "5", Type = ParamType.Number, Group = "预处理" },
                    new ModuleParameter { Name = "高斯σ", DefaultValue = "1.2", Type = ParamType.Number, Group = "预处理" }
                },
                Labels = new List<string>
                {
                    "设置预处理参数，用于后续边缘与测量步骤"
                }
            });

            // ========== 边缘提取（示例） ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.DemoCalculation,
                DisplayName = "边缘提取",
                ModuleName = "",
                ModulePath = "",
                ModuleType = ModuleType.None,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "Canny低阈值", DefaultValue = "60", Type = ParamType.Number, Group = "边缘" },
                    new ModuleParameter { Name = "Canny高阈值", DefaultValue = "120", Type = ParamType.Number, Group = "边缘" },
                    new ModuleParameter { Name = "膨胀次数", DefaultValue = "1", Type = ParamType.Number, Group = "边缘" }
                },
                OutputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "边缘像素数", DefaultValue = "", Type = ParamType.Number, IsReadOnly = true }
                },
                Labels = new List<string>
                {
                    "设置边缘提取参数，执行后输出边缘像素数"
                }
            });

            // ========== 测量与判定（示例） ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.DemoSummary,
                DisplayName = "测量与判定",
                ModuleName = "",
                ModulePath = "",
                ModuleType = ModuleType.None,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "灰度均值下限", DefaultValue = "60", Type = ParamType.Number, Group = "判定阈值" },
                    new ModuleParameter { Name = "灰度均值上限", DefaultValue = "200", Type = ParamType.Number, Group = "判定阈值" },
                    new ModuleParameter { Name = "边缘像素数下限", DefaultValue = "500", Type = ParamType.Number, Group = "判定阈值" },
                    new ModuleParameter { Name = "边缘像素数上限", DefaultValue = "20000", Type = ParamType.Number, Group = "判定阈值" }
                },
                OutputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "灰度均值", DefaultValue = "", Type = ParamType.Number, IsReadOnly = true },
                    new ModuleParameter { Name = "灰度标准差", DefaultValue = "", Type = ParamType.Number, IsReadOnly = true },
                    new ModuleParameter { Name = "边缘像素数", DefaultValue = "", Type = ParamType.Number, IsReadOnly = true },
                    new ModuleParameter { Name = "宽度", DefaultValue = "", Type = ParamType.Number, IsReadOnly = true },
                    new ModuleParameter { Name = "高度", DefaultValue = "", Type = ParamType.Number, IsReadOnly = true }
                },
                Labels = new List<string>
                {
                    "根据阈值判断OK/NG，并输出结构化指标"
                }
            });

            // ========== PKG增亮 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.PkgEnhance,
                DisplayName = "PKG增亮",
                ModuleName = "PKG增亮",
                ModulePath = "校准.PKG增亮",
                ModuleType = ModuleType.ImageEnhance,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "PKG增益",
                        DefaultValue = "20",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "PKG增益"
                    }
                }
            });

            // ========== PKG位置匹配 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.PkgMatching,
                DisplayName = "PKG位置匹配",
                ModuleName = "PKG位置匹配",
                ModulePath = "校准.PKG匹配",
                ModuleType = ModuleType.FeatureMatch,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "PKG匹配阈值",
                        DefaultValue = "0.8",
                        Type = ParamType.Number,
                        GlobalVariableName = "PKG匹配阈值"
                    },
                    new ModuleParameter { Name = "匹配模板路径", DefaultValue = "", Type = ParamType.FilePath }
                },
                Actions = new List<ModuleAction>
                {
                    new ModuleAction { Name = "浏览模板", Handler = null }
                },
                Labels = new List<string>
                {
                    "请设置PKG匹配阈值并选择高精度匹配模板文件（.hpmxml），然后点击运行按钮"
                }
            });

            // ========== PKG测角 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.PkgAngleMeasure,
                DisplayName = "PKG测角",
                ModuleName = "PKG测角",
                ModulePath = "校准.PKG测角",
                ModuleType = ModuleType.LineFind,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "PKG边缘阈值",
                        DefaultValue = "60",
                        Type = ParamType.Number,
                        Group = "阈值设定",
                        GlobalVariableName = "PKG边缘阈值"
                    }
                },
                Labels = new List<string>
                {
                    "请将栅尺至于PKG左外边界，栅尺长度不要超过PKG，栅尺高度不要超过PKG边缘"
                }
            });

            // ========== PKG分离 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.PKGSeparation,
                DisplayName = "PKG分离",
                ModuleName = "PKG分离",
                ModulePath = "校准.PKG二值分离",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "PKG二值化阈值",
                        DefaultValue = "128",
                        Type = ParamType.Number,
                        Group = "分离参数",
                        GlobalVariableName = "PKG二值化阈值"
                    },
                    new ModuleParameter
                    {
                        Name = "PKG腐蚀强度",
                        DefaultValue = "10",
                        Type = ParamType.Number,
                        Group = "分离参数",
                        GlobalVariableName = "PKG腐蚀强度"
                    }
                },
                Labels = new List<string>
                {
                    "请调节二值化阈值使得PKG与其他分离"
                }
            });

            // ========== PKG拉胶检测 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.PKGPointDetection,
                DisplayName = "PKG拉胶",
                ModuleName = "PKG拉胶检测",
                ModulePath = "校准.输出拉胶",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "缩小检测范围",
                        DefaultValue = "3",
                        Type = ParamType.Number,
                        Group = "分离参数",
                        GlobalVariableName = "缩小检测范围"
                    },
                    new ModuleParameter
                    {
                        Name = "拉胶灰度阈值",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "分离参数",
                        GlobalVariableName = "拉胶灰度阈值"
                    },
                    new ModuleParameter
                    {
                        Name = "拉胶面积阈值",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "分离参数",
                        GlobalVariableName = "拉胶面积阈值"
                    }
                }
            });

            // ========== PKG边缘尺寸 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.PkgEdgeSize,
                DisplayName = "PKG边缘尺寸",
                ModuleName = "PKG边缘尺寸",
                ModulePath = "校准.PKG内边渲染",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    // C值基准设定相关参数
                    new ModuleParameter
                    {
                        Name = "C值使用PAD基准",
                        DefaultValue = "false",
                        Type = ParamType.Boolean,
                        Group = "C值参考设定",
                        GlobalVariableName = "C值使用PAD基准",
                        ConversionFunc = BoolToNumber
                    },
                    new ModuleParameter
                    {
                        Name = "PAD_ROI高",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "C值参考设定",
                        GlobalVariableName = "PAD_ROI高"
                    },
                    new ModuleParameter
                    {
                        Name = "PKG_PAD距离",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "C值参考设定",
                        GlobalVariableName = "PKG_PAD距离"
                    },
                    // ROI设定
                    new ModuleParameter
                    {
                        Name = "PKG基准边（左，下）ROI高度(pix)",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "PKG_基准边_ROI高度_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "PKG对边（右，上）ROI高度(pix)",
                        DefaultValue = "30",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "PKG_对边_ROI高度_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "PKG左内边缘中心X(pix)",
                        DefaultValue = "300",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "PKG_左内边缘中心X_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "PKG左ROI宽度(pix)",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "PKG_左ROI宽度_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "PKG下内边缘中心Y(pix)",
                        DefaultValue = "300",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "PKG_下内边缘中心Y_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "PKG下ROI宽度(pix)",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "PKG_下ROI宽度_pix"
                    },
                    // 基准值设定
                    new ModuleParameter
                    {
                        Name = "PKG长边(mm)",
                        DefaultValue = "2.5",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "PKG_设定_长度_pix",
                        ConversionFunc = MmToPixelStr
                    },
                    new ModuleParameter
                    {
                        Name = "PKG短边(mm)",
                        DefaultValue = "1.6",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "PKG_设定_宽度_pix",
                        ConversionFunc = MmToPixelStr
                    }
                }
            });

            // ========== 晶片位置与尺寸 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.ChipPositionSize,
                DisplayName = "晶片位置与尺寸",
                ModuleName = "晶片位置与尺寸",
                ModulePath = "校准.BLK渲染",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "晶片增益",
                        DefaultValue = "0",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "晶片增益"
                    },
                    new ModuleParameter
                    {
                        Name = "自动寻BLK",
                        DefaultValue = "false",
                        Type = ParamType.Boolean,
                        Group = "ROI设定",
                        GlobalVariableName = "自动寻BLK",
                        ConversionFunc = BoolToNumber
                    },

                    new ModuleParameter
                    {
                        Name = "胶点边同时增益",
                        DefaultValue = "true",
                        Type = ParamType.Boolean,
                        Group = "ROI设定",
                        GlobalVariableName = "胶点边同时增益",
                        ConversionFunc = BoolToNumber
                    },

                    new ModuleParameter
                    {
                        Name = "BLK边缘阈值",
                        DefaultValue = "30",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "BLK边缘阈值"
                    },
                    new ModuleParameter
                    {
                        Name = "AI搜索框高度",
                        DefaultValue = "15",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "AI搜索框高度"
                    },
                    new ModuleParameter
                    {
                        Name = "BLK基准边（左，下）ROI高度(pix)",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "BLK_基准边_ROI高度_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "BLK对边（右，上）ROI高度(pix)",
                        DefaultValue = "30",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "BLK_对边_ROI高度_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "BLK左ROI宽度(pix)",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "BLK_左ROI宽度_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "BLK下边缘中心X(pix)",
                        DefaultValue = "300",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "BLK_下边缘中心X_pix"
                    },
                    new ModuleParameter
                    {
                        Name = "BLK下ROI宽度(pix)",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "BLK_下ROI宽度_pix"
                    },
                    // 基准值设定
                    new ModuleParameter
                    {
                        Name = "晶片长(um)",
                        DefaultValue = "600",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "BLK_设定_长度_pix",
                        ConversionFunc = UmToPixelStr
                    },
                    new ModuleParameter
                    {
                        Name = "晶片宽(um)",
                        DefaultValue = "500",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "BLK_设定_宽度_pix",
                        ConversionFunc = UmToPixelStr
                    },
                    // 上下限设定
                    new ModuleParameter
                    {
                        Name = "晶片尺寸公差(±um)",
                        DefaultValue = "16",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "BLK_尺寸公差_pix",
                        ConversionFunc = UmToPixelStr
                    },
                    new ModuleParameter
                    {
                        Name = "BLK-PKG_Y公差(±um)",
                        DefaultValue = "30",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "BLK_PKG_Y_公差_pix",
                        ConversionFunc = UmToPixelStr
                    },
                    new ModuleParameter
                    {
                        Name = "BLK-PKG_X公差(±um)",
                        DefaultValue = "30",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "BLK_PKG_X_公差_pix",
                        ConversionFunc = UmToPixelStr
                    },
                    new ModuleParameter
                    {
                        Name = "BLK-PKG_角度公差(±°)",
                        DefaultValue = "1.5",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "BLK_PKG_角度_公差"
                    },
                    new ModuleParameter
                    {
                        Name = "BLK-PKG_距离(um)",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "BLK_PKG_设定_距离_Y_pix",
                        ConversionFunc = UmToPixelStr
                    },
                    new ModuleParameter
                    {
                        Name = "PAD-BLK_距离下限(um)",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "PAD_BLK_下限_um"
                    },
                    new ModuleParameter
                    {
                        Name = "PAD-BLK_距离上限(um)",
                        DefaultValue = "200",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "PAD_BLK_上限_um"
                    },
                    // 补偿设定
                    new ModuleParameter
                    {
                        Name = "C值补偿(um)",
                        DefaultValue = "0",
                        Type = ParamType.Number,
                        Group = "补偿设定",
                        GlobalVariableName = "C值补偿_um"
                    }
                }
            });

            // ========== 镀膜PKG增亮 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.CoatingPkgEnhance,
                DisplayName = "镀膜PKG增亮",
                ModuleName = "镀膜PKG增亮",
                ModulePath = "校准.镀膜PKG增亮",
                ModuleType = ModuleType.ImageEnhance,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "镀膜PKG增益",
                        DefaultValue = "20",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "镀膜PKG增益"
                    }
                }
            });

            // ========== 镀膜PKG位置匹配 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.CoatingPkgMatching,
                DisplayName = "镀膜PKG位置匹配",
                ModuleName = "异图PKG匹配",
                ModulePath = "校准.异图PKG匹配",
                ModuleType = ModuleType.FeatureMatch,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "镀膜PKG模板路径", DefaultValue = "", Type = ParamType.FilePath }
                },
                Actions = new List<ModuleAction>
                {
                    new ModuleAction { Name = "浏览模板", Handler = null },
                    new ModuleAction { Name = "沿用PKG模板", Handler = null }
                },
                Labels = new List<string>
                {
                    "可沿用PKG位置匹配的模板路径，也可单独设置镀膜图模板路径"
                }
            });

            // ========== 镀膜PKG测角 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.CoatingPkgAngleMeasure,
                DisplayName = "镀膜PKG测角",
                ModuleName = "镀膜PKG边界查找",
                ModulePath = "校准.镀膜PKG边界查找",
                ModuleType = ModuleType.LineFind,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "启用镀膜检测",
                        DefaultValue = "true",
                        Type = ParamType.Boolean,
                        GlobalVariableName = "镀膜使能",
                        ConversionFunc = BoolToNumber
                    }
                }
            });

            // ========== 晶片增亮 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.ChipEnhance,
                DisplayName = "晶片增亮",
                ModuleName = "晶片增亮",
                ModulePath = "校准.晶片增亮",
                ModuleType = ModuleType.ImageEnhance,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "晶片增益",
                        DefaultValue = "0",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "晶片增益"
                    }
                }
            });

            // ========== BLK位置匹配 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.BlkMatching,
                DisplayName = "BLK位置匹配",
                ModuleName = "BLK位置匹配",
                ModulePath = "校准.BLK匹配",
                ModuleType = ModuleType.FeatureMatch,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "BLK匹配模板路径", DefaultValue = "", Type = ParamType.FilePath }
                },
                Actions = new List<ModuleAction>
                {
                    new ModuleAction { Name = "浏览BLK模板", Handler = null }
                }
            });

            // ========== 镀膜匹配 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.CoatingMatching,
                DisplayName = "镀膜匹配",
                ModuleName = "镀膜匹配",
                ModulePath = "校准.纯镀膜匹配",
                ModuleType = ModuleType.FeatureMatch,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "镀膜匹配模板路径", DefaultValue = "", Type = ParamType.FilePath }
                },
                Actions = new List<ModuleAction>
                {
                    new ModuleAction { Name = "浏览镀膜模板", Handler = null }
                }
            });

            // ========== 银面图晶片定位（镀膜晶片增益） ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.CoatingChipEnhance,
                DisplayName = "银面图晶片定位",
                ModuleName = "镀膜晶片增益",
                ModulePath = "校准.镀膜BLK渲染",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "启用镀膜检测",
                        DefaultValue = "true",
                        Type = ParamType.Boolean,
                        GlobalVariableName = "镀膜使能",
                        ConversionFunc = BoolToNumber
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜BLK增益",
                        DefaultValue = "10",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "镀膜BLK增益"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜BLK边缘阈值",
                        DefaultValue = "30",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "镀膜BLK边缘阈值"
                    }
                }
            });

            // ========== 银面几何尺寸（镀膜几何尺寸） ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.CoatingGeometrySize,
                DisplayName = "银面几何尺寸",
                ModuleName = "镀膜几何尺寸",
                ModulePath = "校准.镀膜基准匹配",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "镀膜对比度",
                        DefaultValue = "300",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "镀膜对比度"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜滤波强度",
                        DefaultValue = "5",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "镀膜滤波强度"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜边缘阈值",
                        DefaultValue = "30",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "镀膜边缘阈值"
                    },
                    // 基准值设定
                    new ModuleParameter
                    {
                        Name = "镀膜设定长度",
                        DefaultValue = "1000",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "镀膜设定长度"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜设定宽度",
                        DefaultValue = "600",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "镀膜设定宽度"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜设定中心X",
                        DefaultValue = "600",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "镀膜设定中心X"
                    },
                    // ROI设定
                    new ModuleParameter
                    {
                        Name = "镀膜边界ROI高",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "镀膜边界ROI高"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜G1端偏移",
                        DefaultValue = "0",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "镀膜G1端偏移"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜G1端ROI宽",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "镀膜G1端ROI宽"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜G2端ROI宽",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "镀膜G2端ROI宽"
                    },
                    // 上下限设定
                    new ModuleParameter
                    {
                        Name = "镀膜长宽公差",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "镀膜长宽公差"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜XY公差",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "镀膜XY公差"
                    },
                    new ModuleParameter
                    {
                        Name = "镀膜角度公差",
                        DefaultValue = "3.5",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "镀膜角度公差"
                    }
                }
            });

            // ========== 主振瑕疵 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.MainDefect,
                DisplayName = "主振瑕疵",
                ModuleName = "主振瑕疵",
                ModulePath = "校准.主振瑕疵",
                ModuleType = ModuleType.BlobFind,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "主振瑕疵面积阈值",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        GlobalVariableName = "主振瑕疵面积阈值"
                    }
                },
                Labels = new List<string>
                {
                    "设置主振瑕疵检测的面积阈值，然后点击运行按钮执行检测"
                }
            });

            // ========== 上引脚瑕疵 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.UpperPinDefect,
                DisplayName = "上引脚瑕疵",
                ModuleName = "上引脚瑕疵",
                ModulePath = "校准.上引脚瑕疵",
                ModuleType = ModuleType.BlobFind,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "引脚瑕疵面积阈值",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        GlobalVariableName = "引脚瑕疵面积阈值"
                    }
                },
                Labels = new List<string>
                {
                    "设置上引脚瑕疵检测的面积阈值，然后点击运行按钮执行检测"
                }
            });

            // ========== 下引脚瑕疵 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.LowerPinDefect,
                DisplayName = "下引脚瑕疵",
                ModuleName = "下引脚瑕疵",
                ModulePath = "校准.下引脚瑕疵",
                ModuleType = ModuleType.BlobFind,
                InputParameters = new List<ModuleParameter>(),
                Labels = new List<string>
                {
                    "执行下引脚瑕疵检测（使用上引脚瑕疵的面积阈值），点击运行按钮执行检测"
                }
            });

            // ========== 上胶点检测 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.UpperGluePointDetection,
                DisplayName = "胶点尺寸检测",
                ModuleName = "双胶点轮廓",
                ModulePath = "校准.双胶点轮廓",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    // 基准值设定
                    new ModuleParameter
                    {
                        Name = "胶点设定幅X",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "胶点设定高度"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点设定面积",
                        DefaultValue = "10",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "胶点设定面积"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点设定宽度",
                        DefaultValue = "0",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "胶点设定宽度"
                    },
                    // 图像参数设定
                    new ModuleParameter
                    {
                        Name = "胶点对比度",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "胶点对比度"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点边缘阈值",
                        DefaultValue = "20",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "胶点边缘阈值"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点概率阈值",
                        DefaultValue = "60",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "胶点概率阈值"
                    },
                    // ROI设定
                    new ModuleParameter
                    {
                        Name = "AI胶点",
                        DefaultValue = "false",
                        Type = ParamType.Boolean,
                        Group = "ROI设定",
                        GlobalVariableName = "AI胶点",
                        ConversionFunc = BoolToNumber
                    },
                    new ModuleParameter
                    {
                        Name = "胶点X检测范围",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "胶点X检测范围"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点Y检测范围",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "胶点Y检测范围"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点Y缩进",
                        DefaultValue = "5",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "胶点Y缩进"
                    },
                    // 上下限设定
                    new ModuleParameter
                    {
                        Name = "胶点高度公差",
                        DefaultValue = "10",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "胶点高度公差"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点宽度公差",
                        DefaultValue = "20",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "胶点宽度公差"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点面积公差",
                        DefaultValue = "20",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "胶点面积公差"
                    },
                    new ModuleParameter
                    {
                        Name = "双胶点面积差值",
                        DefaultValue = "20",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "双胶点面积差值"
                    },
                    // 补偿设定
                    new ModuleParameter
                    {
                        Name = "胶点圆拟合",
                        DefaultValue = "true",
                        Type = ParamType.Boolean,
                        Group = "ROI设定",
                        GlobalVariableName = "胶点圆拟合使能",
                        ConversionFunc = BoolToNumber
                    },

                    new ModuleParameter
                    {
                        Name = "幅X补偿",
                        DefaultValue = "0",
                        Type = ParamType.Number,
                        Group = "补偿设定",
                        GlobalVariableName = "幅Y补偿" //按客户要求修订，不是错误匹配
                    },
                    new ModuleParameter
                    {
                        Name = "幅Y补偿",
                        DefaultValue = "0",
                        Type = ParamType.Number,
                        Group = "补偿设定",
                        GlobalVariableName = "幅X补偿" //按客户要求修订，不是错误匹配
                    }
                }
            });

            // ========== 下胶点检测 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.LowerGluePointDetection,
                DisplayName = "胶点位置检测",
                ModuleName = "下胶点检测",
                ModulePath = "校准.双胶点图",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "双胶点设定间距",
                        DefaultValue = "500",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "双胶点设定间距"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点设定左边距",
                        DefaultValue = "200",
                        Type = ParamType.Number,
                        Group = "基准值设定",
                        GlobalVariableName = "胶点设定左边距"
                    },
                    new ModuleParameter
                    {
                        Name = "双胶点间距公差",
                        DefaultValue = "200",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "双胶点间距公差"
                    },
                    new ModuleParameter
                    {
                        Name = "胶点左边距公差",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "上下限设定",
                        GlobalVariableName = "胶点左边距公差"
                    }
                }
            });

            // ========== BLK破损检测 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.BlkDamageDetection,
                DisplayName = "BLK破损检测",
                ModuleName = "BLK破损检测",
                ModulePath = "校准.破损blob",
                ModuleType = ModuleType.BlobFind,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "破损检测边框内缩",
                        DefaultValue = "5",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "破损检测边框内缩"
                    },
                    new ModuleParameter
                    {
                        Name = "破损灰度阈值",
                        DefaultValue = "150",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "破损灰度阈值"
                    },
                    new ModuleParameter
                    {
                        Name = "破损面积阈值",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "破损面积阈值"
                    }
                }
            });

            // ========== 划痕检测 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.ScratchDetection,
                DisplayName = "划痕检测",
                ModuleName = "划痕检测",
                ModulePath = "校准.划痕检测图",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "划痕对比度",
                        DefaultValue = "100",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "划痕对比度"
                    },
                    new ModuleParameter
                    {
                        Name = "检测框缩进",
                        DefaultValue = "5",
                        Type = ParamType.Number,
                        Group = "ROI设定",
                        GlobalVariableName = "检测框缩进"
                    },
                    new ModuleParameter
                    {
                        Name = "划痕亮度阈值",
                        DefaultValue = "150",
                        Type = ParamType.Number,
                        Group = "图像参数设定",
                        GlobalVariableName = "划痕亮度阈值"
                    },
                    new ModuleParameter
                    {
                        Name = "周长阈值",
                        DefaultValue = "50",
                        Type = ParamType.Number,
                        Group = "检测参数",
                        GlobalVariableName = "周长阈值"
                    }
                }
            });

            // ========== 深度破损检测 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.DeepDamageDetection,
                DisplayName = "深度破损检测",
                ModuleName = "深度破损检测",
                ModulePath = "校准.晶片图分割",
                ModuleType = ModuleType.FlawModuleC,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter
                    {
                        Name = "破损检测使能",
                        DefaultValue = "false",
                        Type = ParamType.Boolean,
                        Group = "基础配置",
                        GlobalVariableName = "破损检测使能",
                        ConversionFunc = BoolToNumber
                    },
                    new ModuleParameter
                    {
                        Name = "破损概率阈值",
                        DefaultValue = "230",
                        Type = ParamType.Number,
                        Group = "基础配置",
                        GlobalVariableName = "破损概率阈值"
                    }
                }
            });

            // ========== 短路检测 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.ShortCircuitDetection,
                DisplayName = "短路检测",
                ModuleName = "短路检测",
                ModulePath = "校准.短路输出",
                ModuleType = ModuleType.SaveImage,
                InputParameters = new List<ModuleParameter>()
            });

            // ========== 3D配置 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.ThreeDConfiguration,
                DisplayName = "3D配置",
                ModuleName = "",
                ModulePath = "",
                ModuleType = ModuleType.None,
                IsSpecialStep = true,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "启用3D检测", DefaultValue = "false", Type = ParamType.Boolean },
                    new ModuleParameter { Name = "项目文件夹", DefaultValue = "", Type = ParamType.FolderPath },
                    new ModuleParameter { Name = "重新编译", DefaultValue = "false", Type = ParamType.Boolean },
                    new ModuleParameter { Name = "胶点设定高度", DefaultValue = "0.02", Type = ParamType.Number, Group = "基准值设定" },
                    new ModuleParameter { Name = "G1设定高度", DefaultValue = "0.07", Type = ParamType.Number, Group = "基准值设定" },
                    new ModuleParameter { Name = "G2设定高度", DefaultValue = "0.06", Type = ParamType.Number, Group = "基准值设定" },
                    new ModuleParameter { Name = "胶点-CoverRing最小间距", DefaultValue = "0.05", Type = ParamType.Number, Group = "基准值设定" },
                    new ModuleParameter { Name = "胶点高度公差(±mm)", DefaultValue = "0.01", Type = ParamType.Number, Group = "上下限设定" },
                    new ModuleParameter { Name = "G1G2公差(±mm)", DefaultValue = "0.01", Type = ParamType.Number, Group = "上下限设定" },
                    new ModuleParameter { Name = "G1-G2下限", DefaultValue = "0", Type = ParamType.Number, Group = "上下限设定" },
                    new ModuleParameter { Name = "G1-G2上限", DefaultValue = "0.07", Type = ParamType.Number, Group = "上下限设定" },
                    new ModuleParameter { Name = "B1B2边缘段差下限", DefaultValue = "0", Type = ParamType.Number, Group = "上下限设定" },
                    new ModuleParameter { Name = "B1B2边缘段差上限", DefaultValue = "0.07", Type = ParamType.Number, Group = "上下限设定" },
                    new ModuleParameter { Name = "双胶点高度差范围(±mm)", DefaultValue = "0.01", Type = ParamType.Number, Group = "上下限设定" },
                    new ModuleParameter { Name = "胶点高度补偿", DefaultValue = "0", Type = ParamType.Number, Group = "补偿值设定" },
                    new ModuleParameter { Name = "G1补偿", DefaultValue = "0", Type = ParamType.Number, Group = "补偿值设定" },
                    new ModuleParameter { Name = "G2补偿", DefaultValue = "0", Type = ParamType.Number, Group = "补偿值设定" },
                    new ModuleParameter { Name = "胶点-CoverRing补偿(±mm)", DefaultValue = "0", Type = ParamType.Number, Group = "补偿值设定" },
                    new ModuleParameter { Name = "段差补偿", DefaultValue = "0", Type = ParamType.Number, Group = "补偿值设定" },
                    // 晶片平面估计
                    new ModuleParameter { Name = "晶片平面估计", DefaultValue = "false", Type = ParamType.Boolean, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "左端高度补偿", DefaultValue = "0", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "右端高度补偿", DefaultValue = "0", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "左端高度上限", DefaultValue = "0.1", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "左端高度下限", DefaultValue = "0", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "右端高度上限", DefaultValue = "0.1", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "右端高度下限", DefaultValue = "0", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "俯仰值上限", DefaultValue = "0.05", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "俯仰值下限", DefaultValue = "-0.05", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "滚转值上限", DefaultValue = "0.05", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "滚转值下限", DefaultValue = "-0.05", Type = ParamType.Number, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "晶片左上启用NG判断", DefaultValue = "true", Type = ParamType.Boolean, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "晶片左下启用NG判断", DefaultValue = "true", Type = ParamType.Boolean, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "晶片右上启用NG判断", DefaultValue = "true", Type = ParamType.Boolean, Group = "晶片平面估计" },
                    new ModuleParameter { Name = "晶片右下启用NG判断", DefaultValue = "true", Type = ParamType.Boolean, Group = "晶片平面估计" }
                },
                Actions = new List<ModuleAction>
                {
                    new ModuleAction
                    {
                        Name = "设置工具参数",
                        Handler = null,
                        BackgroundColor = new SolidColorBrush(Colors.Blue),
                        ForegroundColor = new SolidColorBrush(Colors.White)
                    },
                    new ModuleAction
                    {
                        Name = "设定判定对象",
                        Handler = null,
                        BackgroundColor = new SolidColorBrush(Colors.Orange),
                        ForegroundColor = new SolidColorBrush(Colors.White)
                    },
                    new ModuleAction
                    {
                        Name = "设定输出对象",
                        Handler = null,
                        BackgroundColor = new SolidColorBrush(Colors.Purple),
                        ForegroundColor = new SolidColorBrush(Colors.White)
                    }
                },
                Labels = new List<string>
                {
                    "3D检测配置：设置3D检测工具参数、判定标准和输出对象",
                    "确保激光头已连接且3D系统正常工作"
                }
            });

            // ========== 模板命名 ==========
            RegisterModule(new ModuleDefinition
            {
                StepType = StepType.TemplateName,
                DisplayName = "模板命名",
                ModuleName = "",
                ModulePath = "",
                ModuleType = ModuleType.None,
                IsSpecialStep = true,
                InputParameters = new List<ModuleParameter>
                {
                    new ModuleParameter { Name = "模板名称", DefaultValue = "", Type = ParamType.Text },
                    new ModuleParameter { Name = "备注", DefaultValue = "", Type = ParamType.Text }
                },
                Actions = new List<ModuleAction>
                {
                    new ModuleAction
                    {
                        Name = "保存模板",
                        Handler = null,
                        BackgroundColor = new SolidColorBrush(Colors.Green),
                        ForegroundColor = new SolidColorBrush(Colors.White)
                    }
                }
            });
        }

        #endregion

        #region 获取配置列表的辅助方法

        /// <summary>
        /// 根据样品类型和涂布类型获取步骤配置列表
        /// </summary>
        public static List<StepConfiguration> GetStepConfigurations(SampleType sampleType, CoatingType coatingType = CoatingType.Single)
        {
            var profileId = TemplateHierarchyConfig.Instance.ResolveProfileId(sampleType, coatingType);
            var configurations = GetStepConfigurations(profileId);
            if (configurations.Count > 0)
            {
                return configurations;
            }

            return GetLegacyStepConfigurations(sampleType, coatingType);
        }

        public static List<StepConfiguration> GetStepConfigurations(string profileId)
        {
            var profile = TemplateHierarchyConfig.Instance.ResolveProfile(profileId);
            if (profile == null)
            {
                return new List<StepConfiguration>();
            }

            var configurations = new List<StepConfiguration>();
            var stepTypes = profile.GetStepTypes();
            foreach (var stepType in stepTypes)
            {
                var module = GetModule(stepType);
                if (module != null)
                {
                    configurations.Add(module.ToStepConfiguration());
                }
            }

            return configurations;
        }

        private static List<StepConfiguration> GetLegacyStepConfigurations(SampleType sampleType, CoatingType coatingType)
        {
            var configurations = new List<StepConfiguration>();

            // 获取默认流程的步骤类型顺序
            var stepOrder = GetDefaultStepOrder(sampleType);

            foreach (var stepType in stepOrder)
            {
                var module = GetModule(stepType);
                if (module != null)
                {
                    configurations.Add(module.ToStepConfiguration());
                }
            }

            // 如果是双涂布，在适当位置添加胶点检测步骤
            if (coatingType == CoatingType.Double)
            {
                AddGluePointDetectionSteps(configurations);
            }

            return configurations;
        }

        /// <summary>
        /// 获取默认的步骤顺序
        /// </summary>
        private static List<StepType> GetDefaultStepOrder(SampleType sampleType)
        {
            var baseOrder = new List<StepType>
            {
                StepType.ImageSelection,
                StepType.PkgEnhance,
                StepType.PkgMatching,
                StepType.PkgAngleMeasure,
                StepType.PKGSeparation,
                StepType.PKGPointDetection,
                StepType.PkgEdgeSize,
                StepType.ChipPositionSize,
                StepType.CoatingChipEnhance,
                StepType.CoatingGeometrySize
            };

            // MESA类型添加瑕疵检测
            if (sampleType == SampleType.MESA)
            {
                baseOrder.Add(StepType.MainDefect);
                baseOrder.Add(StepType.UpperPinDefect);
                baseOrder.Add(StepType.LowerPinDefect);
            }
            else
            {
                // 其他类型添加划痕检测
                baseOrder.Add(StepType.ScratchDetection);
            }

            // 添加通用的后续步骤
            baseOrder.Add(StepType.BlkDamageDetection);
            baseOrder.Add(StepType.DeepDamageDetection);
            baseOrder.Add(StepType.ThreeDConfiguration);
            baseOrder.Add(StepType.TemplateName);

            return baseOrder;
        }

        /// <summary>
        /// 为双涂布添加胶点检测步骤
        /// </summary>
        private static void AddGluePointDetectionSteps(List<StepConfiguration> configurations)
        {
            int blkDamageIndex = configurations.FindIndex(c => c.StepType == StepType.BlkDamageDetection);
            if (blkDamageIndex >= 0)
            {
                var upperGlueModule = GetModule(StepType.UpperGluePointDetection);
                var lowerGlueModule = GetModule(StepType.LowerGluePointDetection);

                if (upperGlueModule != null)
                {
                    configurations.Insert(blkDamageIndex, upperGlueModule.ToStepConfiguration());
                    blkDamageIndex++;
                }
                if (lowerGlueModule != null)
                {
                    configurations.Insert(blkDamageIndex, lowerGlueModule.ToStepConfiguration());
                }
            }
        }

        /// <summary>
        /// 获取镀膜PKG定位步骤集合
        /// </summary>
        public static List<StepConfiguration> GetCoatingPkgConfigurations()
        {
            var coatingPkgTypes = new List<StepType>
            {
                StepType.CoatingPkgEnhance,
                StepType.CoatingPkgMatching,
                StepType.CoatingPkgAngleMeasure
            };

            return coatingPkgTypes
                .Select(t => GetModule(t)?.ToStepConfiguration())
                .Where(c => c != null)
                .ToList();
        }

        /// <summary>
        /// 获取所有参数到全局变量的映射（合并所有模块）
        /// </summary>
        public static Dictionary<string, string> GetAllParameterMappings()
        {
            var allMappings = new Dictionary<string, string>();
            foreach (var module in _allModules)
            {
                foreach (var mapping in module.GetParameterMappings())
                {
                    if (!allMappings.ContainsKey(mapping.Key))
                    {
                        allMappings[mapping.Key] = mapping.Value;
                    }
                }
            }
            return allMappings;
        }

        /// <summary>
        /// 获取所有参数的单位转换函数（合并所有模块）
        /// </summary>
        public static Dictionary<string, Func<string, string>> GetAllParameterConversions()
        {
            var allConversions = new Dictionary<string, Func<string, string>>();
            foreach (var module in _allModules)
            {
                foreach (var conversion in module.GetParameterConversions())
                {
                    if (!allConversions.ContainsKey(conversion.Key))
                    {
                        allConversions[conversion.Key] = conversion.Value;
                    }
                }
            }
            return allConversions;
        }

        /// <summary>
        /// 获取模块映射字典（ModuleName -> ModulePath, ModuleType）
        /// </summary>
        public static Dictionary<string, (string Path, ModuleType Type)> GetModuleMap()
        {
            var map = new Dictionary<string, (string Path, ModuleType Type)>();
            foreach (var module in _allModules)
            {
                if (!string.IsNullOrEmpty(module.ModuleName) && !string.IsNullOrEmpty(module.ModulePath))
                {
                    map[module.ModuleName] = (module.ModulePath, module.ModuleType);
                }
            }
            return map;
        }

        private static readonly Dictionary<string, SmartInputParameterDisplayConfig> _smartInputParameterDisplayConfigs =
            new Dictionary<string, SmartInputParameterDisplayConfig>
            {
                ["PKG设定宽度"] = new SmartInputParameterDisplayConfig
                {
                    Title = "PKG设定宽度",
                    Description = "设置PKG（封装）的标准宽度值。此参数影响封装尺寸检测的基准，过小会导致误判为尺寸不足，过大会漏检尺寸异常。建议根据产品规格书设置，通常精度要求在±0.01mm以内。",
                    ImagePath = "pkg_width_setting.png",
                    Unit = "mm",
                    MinValue = 0.1,
                    MaxValue = 50.0
                },
                ["PKG设定高度"] = new SmartInputParameterDisplayConfig
                {
                    Title = "PKG设定高度",
                    Description = "设置PKG封装的标准高度值。此参数用于垂直方向的尺寸测量基准。设置过严格会增加误判率，设置过宽松会降低检测精度。建议结合实际产品尺寸和工艺公差来设定。",
                    ImagePath = "pkg_height_setting.png",
                    Unit = "mm",
                    MinValue = 0.1,
                    MaxValue = 50.0
                },
                ["PKG宽度公差"] = new SmartInputParameterDisplayConfig
                {
                    Title = "PKG宽度公差",
                    Description = "PKG宽度测量的允许偏差范围。此参数决定了宽度检测的严格程度，公差过小会导致良品误判为不良，公差过大会漏检不良品。建议根据产品质量要求和工艺能力设置。",
                    ImagePath = "pkg_width_tolerance.png",
                    Unit = "mm",
                    MinValue = 0.001,
                    MaxValue = 5.0
                },
                ["PKG高度公差"] = new SmartInputParameterDisplayConfig
                {
                    Title = "PKG高度公差",
                    Description = "PKG高度测量的允许偏差范围。控制垂直方向的尺寸检测精度，需要根据封装工艺的稳定性来设定。过严会增加误判，过松会降低质量管控效果。",
                    ImagePath = "pkg_height_tolerance.png",
                    Unit = "mm",
                    MinValue = 0.001,
                    MaxValue = 5.0
                },
                ["晶片设定宽度"] = new SmartInputParameterDisplayConfig
                {
                    Title = "晶片设定宽度",
                    Description = "设置芯片（Die）的标准宽度值。此参数是芯片尺寸检测的基准，影响芯片贴装位置和尺寸的判定。设置时需要考虑芯片切割精度和贴装工艺精度。",
                    ImagePath = "chip_width_setting.png",
                    Unit = "μm",
                    MinValue = 10.0,
                    MaxValue = 20000.0
                },
                ["晶片设定高度"] = new SmartInputParameterDisplayConfig
                {
                    Title = "晶片设定高度",
                    Description = "设置芯片的标准高度值。用于垂直方向的芯片尺寸检测基准。此参数直接影响芯片尺寸合格率的判定，需要根据芯片规格和切割工艺精度来设定。",
                    ImagePath = "chip_height_setting.png",
                    Unit = "μm",
                    MinValue = 10.0,
                    MaxValue = 20000.0
                },
                ["胶点设定直径"] = new SmartInputParameterDisplayConfig
                {
                    Title = "胶点设定直径",
                    Description = "设置胶点的标准直径值。此参数是胶点尺寸检测的基准，直接影响点胶质量的判定。胶点过小可能导致粘接强度不足，过大会造成材料浪费。",
                    ImagePath = "glue_point_diameter.png",
                    Unit = "μm",
                    MinValue = 10.0,
                    MaxValue = 5000.0
                },
                ["胶点设定高度"] = new SmartInputParameterDisplayConfig
                {
                    Title = "胶点设定高度",
                    Description = "设置胶点的标准高度值。胶点高度影响粘接效果和固化性能，过低可能导致粘接强度不足，过高会影响后续工艺。",
                    ImagePath = "glue_point_height.png",
                    Unit = "μm",
                    MinValue = 1.0,
                    MaxValue = 1000.0
                },
                ["检测区域X坐标"] = new SmartInputParameterDisplayConfig
                {
                    Title = "检测区域X坐标",
                    Description = "设置检测区域的水平位置坐标。此参数定义了图像处理的感兴趣区域（ROI），影响检测算法的执行范围。正确设置可以提高检测效率和精度。",
                    ImagePath = "detection_area_x.png",
                    Unit = "pixel",
                    MinValue = 0,
                    MaxValue = 4096
                },
                ["检测区域Y坐标"] = new SmartInputParameterDisplayConfig
                {
                    Title = "检测区域Y坐标",
                    Description = "设置检测区域的垂直位置坐标。与X坐标配合定义完整的检测区域，影响算法处理的图像范围。合理设置可以排除无关区域。",
                    ImagePath = "detection_area_y.png",
                    Unit = "pixel",
                    MinValue = 0,
                    MaxValue = 4096
                },
                ["阈值设定"] = new SmartInputParameterDisplayConfig
                {
                    Title = "图像阈值设定",
                    Description = "设置图像二值化的阈值参数。此参数决定了图像分割的效果，影响目标识别的准确性。阈值过低会产生过多噪点，过高会丢失目标信息。",
                    ImagePath = "threshold_setting.png",
                    Unit = "",
                    MinValue = 0,
                    MaxValue = 255
                }
            };

        /// <summary>
        /// 获取智能输入的参数展示配置（标题/描述/图片/单位/范围）
        /// </summary>
        public static SmartInputParameterDisplayConfig GetSmartInputParameterDisplayConfig(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return new SmartInputParameterDisplayConfig();
            }

            if (_smartInputParameterDisplayConfigs.TryGetValue(parameterName, out var config))
            {
                return new SmartInputParameterDisplayConfig
                {
                    Title = config.Title,
                    Description = config.Description,
                    ImagePath = config.ImagePath,
                    Unit = config.Unit,
                    MinValue = config.MinValue,
                    MaxValue = config.MaxValue
                };
            }

            return new SmartInputParameterDisplayConfig
            {
                Title = parameterName,
                Description = $"设置{parameterName}的数值。请根据实际需求调整此参数。",
                ImagePath = "default_parameter.png",
                Unit = "mm",
                MinValue = 0.0,
                MaxValue = 100.0
            };
        }

        #endregion
    }
}

