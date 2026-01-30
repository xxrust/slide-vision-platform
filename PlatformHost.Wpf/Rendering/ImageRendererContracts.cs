using System.Windows.Controls;
using VMControls.WPF.Release;

namespace WpfApp2.Rendering
{
    public static class ImageRendererIds
    {
        public const string Vm = "VM";
        public const string File = "File";
    }

    public sealed class ImageRendererContext
    {
        public VmRenderControl VmRender1 { get; set; }
        public VmRenderControl VmRender2_1 { get; set; }
        public VmRenderControl VmRender2_2 { get; set; }
        public Image PreviewImage1 { get; set; }
        public Image PreviewImage2_1 { get; set; }
        public Image PreviewImage2_2 { get; set; }
    }

    public interface IImageRenderer
    {
        string RendererId { get; }
        void Bind(ImageRendererContext context);
        void DisplayImageGroup(WpfApp2.UI.ImageGroupSet group);
        void Clear();
    }
}
