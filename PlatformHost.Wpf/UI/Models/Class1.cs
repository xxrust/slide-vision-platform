using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GlueInspect.Algorithm.Contracts;
using Newtonsoft.Json;

namespace WpfApp2.Models
{
    /// <summary>
    /// 样品类型枚举
    /// </summary>
    public enum SampleType
    {
        MESA = 0,           // 抛光，内部凹陷
        Other = 2           // 其他类型(原正常磨砂)
    }

    /// <summary>
    /// 涂布类型枚举
    /// </summary>
    public enum CoatingType
    {
        Single = 1,         // 单涂布
        Double = 2          // 双涂布
    }

    /// <summary>
    /// 步骤类型枚举，用于标识不同的配置步骤
    /// </summary>
    public enum StepType
    {
        ImageSelection,     // 图片选择
        PkgEnhance,        // PKG增亮
        PkgMatching,       // PKG位置匹配
        PkgAngleMeasure,   // PKG测角
        PkgEdgeSize,       // PKG边缘尺寸
        CoatingPkgEnhance,   // 镀膜PKG增亮
        CoatingPkgMatching,  // 镀膜PKG位置匹配
        CoatingPkgAngleMeasure, // 镀膜PKG测角
        
        ChipEnhance,       // 晶片增亮
        BlkMatching,       // BLK位置匹配
        ChipPositionSize,  // 晶片位置与尺寸
        CoatingMatching,   // 镀膜匹配
        MainDefect,        // 主振瑕疵
        UpperPinDefect,    // 上引脚瑕疵
        LowerPinDefect,    // 下引脚瑕疵
        CoatingChipEnhance,    // 镀膜增亮
        CoatingChipSize,   // 镀膜晶片及尺寸
        CoatingGeometrySize, // 镀膜几何尺寸
        UpperGluePointDetection,  // 上胶点检测
        LowerGluePointDetection,  // 下胶点检测
        ShortCircuitDetection,    // 短路检测
        PKGSeparation,     // PKG分离
        PKGPointDetection, // PKG拉胶检测
        BlkDamageDetection, // BLK破损检测
        ScratchDetection,  // 划痕检测
        DeepDamageDetection, // 深度破损检测
        ThreeDConfiguration, // 3D配置
        TemplateName       // 模板命名
    }

    /// <summary>
    /// 样品类型信息类
    /// </summary>
    public class SampleTypeInfo
    {
        public SampleType Type { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// 相机参数配置类
    /// </summary>
    public class CameraParameters
    {
        /// <summary>
        /// 飞拍相机曝光时间(us)
        /// </summary>
        public int FlyingExposureTime { get; set; } = 8;

        /// <summary>
        /// 飞拍相机延迟时间(us)
        /// </summary>
        public int FlyingDelayTime { get; set; } = 0;

        /// <summary>
        /// 定拍相机1曝光强度(0-255) - 已弃用，现在固定为255
        /// </summary>
        public int Fixed1ExposureIntensity { get; set; } = 255;

        /// <summary>
        /// 定拍相机1曝光时间(us)，范围0-1000
        /// </summary>
        public int Fixed1ExposureTime { get; set; } = 100;

        /// <summary>
        /// 定拍相机2曝光强度(0-255) - 已弃用，现在固定为255
        /// </summary>
        public int Fixed2ExposureIntensity { get; set; } = 255;

        /// <summary>
        /// 定拍相机2曝光时间(us)，范围0-1000
        /// </summary>
        public int Fixed2ExposureTime { get; set; } = 100;

        /// <summary>
        /// 定拍相机1同轴光曝光时间(us)
        /// </summary>
        public int Fixed1CoaxialTime { get; set; } = 0;

        /// <summary>
        /// 定拍相机2同轴光曝光时间(us)
        /// </summary>
        public int Fixed2CoaxialTime { get; set; } = 0;

        /// <summary>
        /// 45度光使能状态
        /// </summary>
        public bool Enable45DegreeLight { get; set; } = true;

        /// <summary>
        /// 0度光使能状态
        /// </summary>
        public bool Enable0DegreeLight { get; set; } = true;

        /// <summary>
        /// LID图像选择：1-飞拍相机，2-定拍相机1，3-定拍相机2
        /// </summary>
        public int LidImageSelection { get; set; } = 2;

        /// <summary>
        /// 镀膜图像选择：1-飞拍相机，3-定拍相机2
        /// </summary>
        public int CoatingImageSelection { get; set; } = 3;
    }

    /// <summary>
    /// 3D检测参数配置类
    /// </summary>
    public class Detection3DParameters
    {
        /// <summary>
        /// 是否启用3D检测功能
        /// </summary>
        public bool Enable3DDetection { get; set; } = false;

        /// <summary>
        /// 3D检测项目名（默认为空，将自动设置为模板名）
        /// </summary>
        public string ProjectName { get; set; } = "";

        /// <summary>
        /// 3D检测项目文件夹
        /// </summary>
        public string ProjectFolder { get; set; } = @"D:\KEYENCE\LJ Navigator\Program";

        /// <summary>
        /// 高度图像路径
        /// </summary>
        public string HeightImagePath { get; set; } = @"E:\posen_project\点胶检测\上位机程序\WpfApp2\MESA检测\RawImage";

        /// <summary>
        /// 是否重新编译
        /// </summary>
        public bool ReCompile { get; set; } = false;
    }

    /// <summary>
    /// 颜色配置参数类
    /// </summary>
    public class ColorConfigParameters
    {
        /// <summary>
        /// 是否使用自定义颜色范围
        /// </summary>
        public bool UseCustomColorRange { get; set; } = false;

        /// <summary>
        /// 颜色范围最小值(mm)
        /// </summary>
        public double ColorRangeMin { get; set; } = 0.0;

        /// <summary>
        /// 颜色范围最大值(mm)
        /// </summary>
        public double ColorRangeMax { get; set; } = 2.5;

        /// <summary>
        /// 网格透明度(0-1)
        /// </summary>
        public float MeshTransparent { get; set; } = 0.5f;

        /// <summary>
        /// 混合权重(0-1)
        /// </summary>
        public float BlendWeight { get; set; } = 0.5f;

        /// <summary>
        /// 显示颜色条
        /// </summary>
        public bool DisplayColorBar { get; set; } = true;

        /// <summary>
        /// 显示网格
        /// </summary>
        public bool DisplayGrid { get; set; } = true;

        /// <summary>
        /// 显示坐标轴
        /// </summary>
        public bool DisplayAxis { get; set; } = true;
    }



    public class TemplateParameters
    {
        /// <summary>
        /// 样品类型
        /// </summary>
        public SampleType SampleType { get; set; } = SampleType.Other;

        /// <summary>
        /// 涂布类型
        /// </summary>
        public CoatingType CoatingType { get; set; } = CoatingType.Single;

        // 存储每个步骤的输入参数，Key为步骤类型(StepType)，Value为该步骤的所有参数
        public Dictionary<StepType, Dictionary<string, string>> InputParameters { get; set; } = new Dictionary<StepType, Dictionary<string, string>>();

        // 相机参数配置
        public CameraParameters CameraParams { get; set; } = new CameraParameters();

        // 3D检测参数配置
        public Detection3DParameters Detection3DParams { get; set; } = new Detection3DParameters();

        // 颜色配置参数
        public ColorConfigParameters ColorParams { get; set; } = new ColorConfigParameters();

        // 模板名称
        public string TemplateName { get; set; }

        // 算法引擎标识（VM/OpenCV/ONNX）
        public string AlgorithmEngineId { get; set; } = AlgorithmEngineIds.Vm;

        // 备注信息
        public string Remark { get; set; }

        // 创建时间
        public DateTime CreatedTime { get; set; }

        // 最后修改时间
        public DateTime LastModifiedTime { get; set; }

        // 保存模板参数到JSON文件
        public void SaveToFile(string filePath)
        {
            // 更新最后修改时间
            LastModifiedTime = DateTime.Now;

            // 序列化为JSON
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);

            // 写入文件
            File.WriteAllText(filePath, json);
        }

        // 从JSON文件加载模板参数
        public static TemplateParameters LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"找不到模板文件: {filePath}");

            string json = File.ReadAllText(filePath);
            var template = JsonConvert.DeserializeObject<TemplateParameters>(json);
            
            // 确保相机参数不为null（兼容旧版本模板文件）
            if (template.CameraParams == null)
            {
                template.CameraParams = new CameraParameters();
            }
            
            // 确保3D检测参数不为null（兼容旧版本模板文件）
            if (template.Detection3DParams == null)
            {
                template.Detection3DParams = new Detection3DParameters();
            }
            
            // 确保颜色配置参数不为null（兼容旧版本模板文件）
            if (template.ColorParams == null)
            {
                template.ColorParams = new ColorConfigParameters();
            }
            
            // 确保InputParameters不为null
            if (template.InputParameters == null)
            {
                template.InputParameters = new Dictionary<StepType, Dictionary<string, string>>();
            }

            // 确保算法引擎有默认值（兼容旧版本模板文件）
            if (string.IsNullOrWhiteSpace(template.AlgorithmEngineId))
            {
                template.AlgorithmEngineId = AlgorithmEngineIds.Vm;
            }
            
            return template;
        }







        // 从当前目录下的Templates文件夹获取所有可用模板
        public static List<TemplateParameters> GetAllTemplates()
        {
            List<TemplateParameters> templates = new List<TemplateParameters>();

            string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            if (!Directory.Exists(templatesDir))
                Directory.CreateDirectory(templatesDir);

            string[] templateFiles = Directory.GetFiles(templatesDir, "*.json");
            foreach (string file in templateFiles)
            {
                try
                {
                    TemplateParameters template = LoadFromFile(file);
                    templates.Add(template);
                }
                catch (Exception ex)
                {
                    // 忽略损坏的模板文件，但可以记录日志
                    System.Diagnostics.Debug.WriteLine($"无法加载模板文件 {file}: {ex.Message}");
                }
            }

            return templates;
        }

        /// <summary>
        /// 获取所有可用的样品类型信息
        /// </summary>
        /// <returns>样品类型信息列表</returns>
        public static List<SampleTypeInfo> GetAllSampleTypes()
        {
            return new List<SampleTypeInfo>
            {
                new SampleTypeInfo
                {
                    Type = SampleType.MESA,
                    DisplayName = "MESA",
                    Description = "抛光，内部凹陷"
                },
                new SampleTypeInfo
                {
                    Type = SampleType.Other,
                    DisplayName = "其他",
                    Description = "除了MESA，抛光和平片都选这类，区别在于相机（光源）参数配置"
                }
            };
        }


        

    }
}

