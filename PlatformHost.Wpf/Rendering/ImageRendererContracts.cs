using System.Windows.Controls;

namespace WpfApp2.Rendering
{
    public static class ImageRendererIds
    {
        public const string File = "File";
    }

    public sealed class ImageRendererContext
    {
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer1 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer2 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer3 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer4 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer5 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer6 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer7 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer8 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer9 { get; set; }
        public WpfApp2.UI.Controls.ImageInspectionViewer PreviewViewer10 { get; set; }
    }

    public interface IImageRenderer
    {
        string RendererId { get; }
        void Bind(ImageRendererContext context);
        void DisplayImageGroup(WpfApp2.UI.ImageGroupSet group);
        void Clear();
    }
}

