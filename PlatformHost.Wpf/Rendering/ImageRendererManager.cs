using System;

namespace WpfApp2.Rendering
{
    public static class ImageRendererManager
    {
        public static IImageRenderer ResolveRenderer(ImageRendererContext context)
        {
            string rendererId = RendererSettingsManager.RendererId;
            if (string.Equals(rendererId, ImageRendererIds.Vm, StringComparison.OrdinalIgnoreCase))
            {
                var renderer = new VmImageRenderer();
                if (renderer.CanRender())
                {
                    return renderer;
                }
            }

            return new FileImageRenderer();
        }
    }
}
