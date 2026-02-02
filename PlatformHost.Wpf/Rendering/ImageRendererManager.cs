namespace WpfApp2.Rendering
{
    public static class ImageRendererManager
    {
        public static IImageRenderer ResolveRenderer(ImageRendererContext context)
        {
            return new FileImageRenderer();
        }
    }
}
