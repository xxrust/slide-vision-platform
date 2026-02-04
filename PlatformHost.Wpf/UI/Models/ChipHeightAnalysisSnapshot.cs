using System.Collections.Generic;

namespace WpfApp2.UI.Models
{
    public class ChipCornerAnalysisItem
    {
        public string Name { get; set; }
        public double X2DPixel { get; set; }
        public double Y2DPixel { get; set; }
        public double X3D { get; set; }
        public double Y3D { get; set; }
        public double ChipHeight { get; set; }
        public double RefHeight { get; set; }
        public double RelativeHeight { get; set; }
    }

    public class ChipHeightAnalysisParams2D
    {
        public double PkgCenterX { get; set; }
        public double PkgCenterY { get; set; }
        public double ChipCenterX { get; set; }
        public double ChipCenterY { get; set; }
        // 来自检测结果的长度/宽度，单位微米，便于前端展示和计算
        public double ChipLengthUm { get; set; }
        public double ChipWidthUm { get; set; }
        public double ChipAngleDeg { get; set; }
    }

    public class ChipHeightAnalysisParams3D
    {
        public double PkgCenterX { get; set; }
        public double PkgCenterY { get; set; }
        public double LineStartX { get; set; }
        public double LineStartY { get; set; }
        public double LineEndX { get; set; }
        public double LineEndY { get; set; }
        public double ChipPlaneA { get; set; }
        public double ChipPlaneB { get; set; }
        public double ChipPlaneC { get; set; }
        public double RefPlaneA { get; set; }
        public double RefPlaneB { get; set; }
        public double RefPlaneC { get; set; }
        public double PkgAngleDeg { get; set; }
    }

    public class ChipHeightAnalysisSnapshot
    {
        public ChipHeightAnalysisParams2D Params2D { get; set; }
        public ChipHeightAnalysisParams3D Params3D { get; set; }
        public List<ChipCornerAnalysisItem> Corners { get; set; } = new List<ChipCornerAnalysisItem>();
        public double PixelSizeMm { get; set; }
        public string GrayImagePath { get; set; }

        public bool Has2D => Params2D != null;
        public bool Has3D => Params3D != null;
        public bool HasCorners => Corners != null && Corners.Count > 0;
        public bool HasGrayImage => !string.IsNullOrEmpty(GrayImagePath);
    }
}

