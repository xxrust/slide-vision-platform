using System;
using System.Windows;
using ImageSourceModuleCs;
using VM.Core;

namespace WpfApp2.Rendering
{
    public sealed class VmImageRenderer : IImageRenderer
    {
        private ImageRendererContext _context;

        public string RendererId => ImageRendererIds.Vm;

        public void Bind(ImageRendererContext context)
        {
            _context = context;
        }

        public bool CanRender()
        {
            try
            {
                return VmSolution.Instance != null;
            }
            catch
            {
                return false;
            }
        }

        public void DisplayImageGroup(WpfApp2.UI.ImageGroupSet group)
        {
            if (_context == null || group == null)
            {
                return;
            }

            if (!CanRender())
            {
                return;
            }

            ShowVmRenderers();

            try
            {
                var imageSource1 = VmSolution.Instance["获取路径图像.图1"] as ImageSourceModuleTool;
                if (imageSource1 != null && !string.IsNullOrWhiteSpace(group.Source1Path))
                {
                    _context.VmRender1.ModuleSource = imageSource1;
                    imageSource1.SetImagePath(group.Source1Path);
                }

                var imageSource2_1 = VmSolution.Instance["获取路径图像.图2_1"] as ImageSourceModuleTool;
                if (imageSource2_1 != null && !string.IsNullOrWhiteSpace(group.Source2_1Path))
                {
                    _context.VmRender2_1.ModuleSource = imageSource2_1;
                    imageSource2_1.SetImagePath(group.Source2_1Path);
                }

                var imageSource2_2 = VmSolution.Instance["获取路径图像.图2_2"] as ImageSourceModuleTool;
                if (imageSource2_2 != null && !string.IsNullOrWhiteSpace(group.Source2_2Path))
                {
                    _context.VmRender2_2.ModuleSource = imageSource2_2;
                    imageSource2_2.SetImagePath(group.Source2_2Path);
                }
            }
            catch
            {
                // 渲染失败时不阻塞主流程
            }
        }

        public void Clear()
        {
            if (_context == null)
            {
                return;
            }

            try
            {
                _context.VmRender1.ModuleSource = null;
                _context.VmRender2_1.ModuleSource = null;
                _context.VmRender2_2.ModuleSource = null;
            }
            catch
            {
                // 忽略清理异常
            }
        }

        private void ShowVmRenderers()
        {
            if (_context?.VmRender1 == null)
            {
                return;
            }

            _context.VmRender1.Visibility = Visibility.Visible;
            _context.VmRender2_1.Visibility = Visibility.Visible;
            _context.VmRender2_2.Visibility = Visibility.Visible;

            if (_context.PreviewImage1 != null) _context.PreviewImage1.Visibility = Visibility.Collapsed;
            if (_context.PreviewImage2_1 != null) _context.PreviewImage2_1.Visibility = Visibility.Collapsed;
            if (_context.PreviewImage2_2 != null) _context.PreviewImage2_2.Visibility = Visibility.Collapsed;
        }
    }
}
