using System;
using System.Windows;
using System.Windows.Media;

namespace WpfApp2.UI.Controls
{
    public sealed class GlueDotParticleFieldControl : FrameworkElement
    {
        public int ParticleCount { get; set; } = 1100;
        public double DotRadiusRatio { get; set; } = 0.19;
        public double DotSeparationRatio { get; set; } = 0.24;
        public double HeightLiftRatio { get; set; } = 0.18;
        public bool EnableGlow { get; set; } = true;

        private readonly Random _random = new Random();
        private Particle[] _particles = Array.Empty<Particle>();
        private bool _isSubscribed;
        private TimeSpan _lastRenderingTime;
        private double _timeSeconds;
        private BrushLut _brushLut;
        private Brush _vignetteBrush;
        private RadialGradientBrush _haloBrush;

        public GlueDotParticleFieldControl()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            IsHitTestVisible = false;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureBrushLut();
            EnsureStaticBrushes();
            EnsureParticles();
            SubscribeRendering();
            InvalidateVisual();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeRendering();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            EnsureParticles();
        }

        private void SubscribeRendering()
        {
            if (_isSubscribed)
            {
                return;
            }

            _lastRenderingTime = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
            _isSubscribed = true;
        }

        private void UnsubscribeRendering()
        {
            if (!_isSubscribed)
            {
                return;
            }

            CompositionTarget.Rendering -= OnRendering;
            _isSubscribed = false;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            RenderingEventArgs renderingArgs = e as RenderingEventArgs;
            if (renderingArgs == null)
            {
                InvalidateVisual();
                return;
            }

            if (_lastRenderingTime == TimeSpan.Zero)
            {
                _lastRenderingTime = renderingArgs.RenderingTime;
                InvalidateVisual();
                return;
            }

            TimeSpan delta = renderingArgs.RenderingTime - _lastRenderingTime;
            _lastRenderingTime = renderingArgs.RenderingTime;

            double deltaSeconds = Math.Max(0.0, Math.Min(0.05, delta.TotalSeconds));
            _timeSeconds += deltaSeconds;

            InvalidateVisual();
        }

        private void EnsureBrushLut()
        {
            if (_brushLut != null)
            {
                return;
            }

            _brushLut = new BrushLut();
        }

        private void EnsureStaticBrushes()
        {
            if (_vignetteBrush == null)
            {
                RadialGradientBrush vignette = new RadialGradientBrush();
                vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
                vignette.GradientStops.Add(new GradientStop(Color.FromArgb(90, 0, 0, 0), 1.0));
                vignette.RadiusX = 0.85;
                vignette.RadiusY = 0.85;
                vignette.Center = new Point(0.5, 0.45);
                vignette.GradientOrigin = vignette.Center;
                vignette.Freeze();
                _vignetteBrush = vignette;
            }

            if (_haloBrush == null)
            {
                Color haloColor = Color.FromArgb(35, 52, 152, 219);
                RadialGradientBrush haloBrush = new RadialGradientBrush();
                haloBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, haloColor.R, haloColor.G, haloColor.B), 0.0));
                haloBrush.GradientStops.Add(new GradientStop(haloColor, 0.55));
                haloBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, haloColor.R, haloColor.G, haloColor.B), 1.0));
                haloBrush.Freeze();
                _haloBrush = haloBrush;
            }
        }

        private void EnsureParticles()
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 1 || height <= 1)
            {
                _particles = Array.Empty<Particle>();
                return;
            }

            int particleCount = Math.Max(200, ParticleCount);
            _particles = new Particle[particleCount];

            int dotParticleCount = (int)(particleCount * 0.88);
            int perDot = dotParticleCount / 2;

            for (int i = 0; i < particleCount; i++)
            {
                Particle particle = new Particle();
                particle.Phase = _random.NextDouble() * Math.PI * 2.0;
                particle.Jitter = _random.NextDouble();
                particle.SizeSeed = _random.NextDouble();

                if (i < perDot)
                {
                    particle.DotIndex = 0;
                    particle.RadiusFactor = Math.Sqrt(_random.NextDouble());
                    particle.Angle = _random.NextDouble() * Math.PI * 2.0;
                    particle.AngularSpeed = 0.55 + 1.35 * _random.NextDouble();
                }
                else if (i < perDot * 2)
                {
                    particle.DotIndex = 1;
                    particle.RadiusFactor = Math.Sqrt(_random.NextDouble());
                    particle.Angle = _random.NextDouble() * Math.PI * 2.0;
                    particle.AngularSpeed = 0.55 + 1.35 * _random.NextDouble();
                }
                else
                {
                    particle.DotIndex = 2;
                    particle.RadiusFactor = _random.NextDouble();
                    particle.Angle = _random.NextDouble() * Math.PI * 2.0;
                    particle.AngularSpeed = 0.15 + 0.45 * _random.NextDouble();
                }

                _particles[i] = particle;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            EnsureBrushLut();
            EnsureStaticBrushes();

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 1 || height <= 1 || _particles.Length == 0)
            {
                return;
            }

            double minSize = Math.Min(width, height);
            double dotRadius = minSize * Math.Max(0.10, Math.Min(0.28, DotRadiusRatio));
            double separation = width * Math.Max(0.12, Math.Min(0.38, DotSeparationRatio));
            double centerY = height * 0.38;

            double driftX = Math.Sin(_timeSeconds * 0.35) * width * 0.010;
            double driftY = Math.Cos(_timeSeconds * 0.22) * height * 0.010;

            Point dotA = new Point(width * 0.5 - separation * 0.5 + driftX, centerY + driftY);
            Point dotB = new Point(width * 0.5 + separation * 0.5 + driftX, centerY - driftY);

            double sigma = dotRadius * 0.58;
            double sigmaSquared = sigma * sigma;
            double heightLift = dotRadius * Math.Max(0.05, Math.Min(0.35, HeightLiftRatio));

            DrawVignette(drawingContext, width, height);
            DrawFieldHalo(drawingContext, dotA, dotB, dotRadius);

            double lightDirX = -0.45;
            double lightDirY = -0.55;
            double lightDirZ = 1.00;
            Normalize(ref lightDirX, ref lightDirY, ref lightDirZ);

            double maxHeight = 1.0;
            double ripple = 0.10;

            for (int i = 0; i < _particles.Length; i++)
            {
                Particle particle = _particles[i];

                Point anchor = particle.DotIndex == 0 ? dotA : particle.DotIndex == 1 ? dotB : Midpoint(dotA, dotB);
                double localRadius = particle.DotIndex == 2 ? dotRadius * 1.9 : dotRadius;

                double angle = particle.Angle + _timeSeconds * particle.AngularSpeed;
                double baseRadius = localRadius * particle.RadiusFactor;

                double wobble = 1.0 + 0.05 * Math.Sin(_timeSeconds * 1.8 + particle.Phase);
                double radius = baseRadius * wobble;

                double jitterScale = (1.0 - particle.RadiusFactor) * localRadius * 0.02;
                double jitterX = jitterScale * Math.Sin(_timeSeconds * 2.1 + particle.Jitter * 9.0);
                double jitterY = jitterScale * Math.Cos(_timeSeconds * 1.9 + particle.Jitter * 7.0);

                Point position = new Point(
                    anchor.X + radius * Math.Cos(angle) + jitterX,
                    anchor.Y + radius * Math.Sin(angle) + jitterY);

                HeightSample sample = SampleHeight(position, dotA, dotB, sigmaSquared, ripple, _timeSeconds);
                double height01 = Clamp01(sample.Height / maxHeight);

                double normalX = -sample.GradX * (dotRadius * 0.75);
                double normalY = -sample.GradY * (dotRadius * 0.75);
                double normalZ = 1.0;
                Normalize(ref normalX, ref normalY, ref normalZ);

                double ndotl = Clamp01(normalX * lightDirX + normalY * lightDirY + normalZ * lightDirZ);
                double brightness = 0.55 + 0.45 * ndotl;
                brightness *= 0.70 + 0.30 * (1.0 - particle.RadiusFactor);

                double ambientMask = particle.DotIndex == 2 ? 0.45 : 1.0;
                double alpha = (0.12 + 0.85 * height01) * ambientMask;
                alpha *= 0.75 + 0.25 * (1.0 - particle.RadiusFactor);

                double sizePx = (0.8 + 1.9 * height01) * (0.75 + 0.55 * (1.0 - particle.RadiusFactor));
                sizePx *= 0.85 + 0.35 * particle.SizeSeed;
                sizePx *= ambientMask;

                Point projected = new Point(position.X, position.Y - height01 * heightLift);

                if (EnableGlow && height01 > 0.18)
                {
                    double glowAlpha = Clamp01(alpha * 0.25);
                    Brush glowBrush = _brushLut.Get(height01, brightness, glowAlpha);
                    drawingContext.DrawEllipse(glowBrush, null, projected, sizePx * 2.8, sizePx * 2.8);
                }

                Brush coreBrush = _brushLut.Get(height01, brightness, Clamp01(alpha));
                drawingContext.DrawEllipse(coreBrush, null, projected, sizePx, sizePx);
            }
        }

        private static HeightSample SampleHeight(Point position, Point dotA, Point dotB, double sigmaSquared, double ripple)
        {
            return SampleHeight(position, dotA, dotB, sigmaSquared, ripple, 0.0);
        }

        private static HeightSample SampleHeight(Point position, Point dotA, Point dotB, double sigmaSquared, double ripple, double timeSeconds)
        {
            HeightSample sample = new HeightSample();

            GaussianContribution a = Gaussian(position, dotA, sigmaSquared, 1.0);
            GaussianContribution b = Gaussian(position, dotB, sigmaSquared, 1.0);

            double wave = ripple * Math.Sin(((position.X + position.Y) * 0.013) + timeSeconds * 1.25);

            sample.Height = a.Value + b.Value + wave;
            sample.GradX = a.GradX + b.GradX;
            sample.GradY = a.GradY + b.GradY;
            return sample;
        }

        private static GaussianContribution Gaussian(Point position, Point center, double sigmaSquared, double peak)
        {
            double dx = position.X - center.X;
            double dy = position.Y - center.Y;
            double d2 = dx * dx + dy * dy;

            double value = peak * Math.Exp(-d2 / (2.0 * sigmaSquared));

            GaussianContribution contribution = new GaussianContribution();
            contribution.Value = value;
            contribution.GradX = -(dx / sigmaSquared) * value;
            contribution.GradY = -(dy / sigmaSquared) * value;
            return contribution;
        }

        private static Point Midpoint(Point a, Point b)
        {
            return new Point((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        private static void Normalize(ref double x, ref double y, ref double z)
        {
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (length <= 1e-9)
            {
                x = 0;
                y = 0;
                z = 1;
                return;
            }

            x /= length;
            y /= length;
            z /= length;
        }

        private void DrawVignette(DrawingContext drawingContext, double width, double height)
        {
            Rect rect = new Rect(0, 0, width, height);
            drawingContext.DrawRectangle(_vignetteBrush, null, rect);
        }

        private void DrawFieldHalo(DrawingContext drawingContext, Point dotA, Point dotB, double dotRadius)
        {
            double haloRadius = dotRadius * 2.8;
            drawingContext.DrawEllipse(_haloBrush, null, dotA, haloRadius, haloRadius);
            drawingContext.DrawEllipse(_haloBrush, null, dotB, haloRadius, haloRadius);
        }

        private struct Particle
        {
            public int DotIndex;
            public double RadiusFactor;
            public double Angle;
            public double AngularSpeed;
            public double Phase;
            public double Jitter;
            public double SizeSeed;
        }

        private struct GaussianContribution
        {
            public double Value;
            public double GradX;
            public double GradY;
        }

        private struct HeightSample
        {
            public double Height;
            public double GradX;
            public double GradY;
        }

        private sealed class BrushLut
        {
            private const int HeightSteps = 48;
            private const int BrightnessSteps = 12;
            private const int AlphaSteps = 12;

            private readonly SolidColorBrush[] _brushes = new SolidColorBrush[HeightSteps * BrightnessSteps * AlphaSteps];

            public Brush Get(double height01, double brightness01, double alpha01)
            {
                int hIndex = Quantize(height01, HeightSteps);
                int bIndex = Quantize(brightness01, BrightnessSteps);
                int aIndex = Quantize(alpha01, AlphaSteps);

                int index = (aIndex * BrightnessSteps + bIndex) * HeightSteps + hIndex;

                SolidColorBrush brush = _brushes[index];
                if (brush != null)
                {
                    return brush;
                }

                Color baseColor = HeightRamp(hIndex / (double)(HeightSteps - 1));
                Color shaded = ApplyBrightness(baseColor, bIndex / (double)(BrightnessSteps - 1));
                byte alpha = (byte)(255.0 * (aIndex / (double)(AlphaSteps - 1)));

                shaded = Color.FromArgb(alpha, shaded.R, shaded.G, shaded.B);
                brush = new SolidColorBrush(shaded);
                brush.Freeze();
                _brushes[index] = brush;
                return brush;
            }

            private static int Quantize(double value01, int steps)
            {
                if (steps <= 1)
                {
                    return 0;
                }

                int index = (int)Math.Round(Clamp01(value01) * (steps - 1));
                if (index < 0) return 0;
                if (index > steps - 1) return steps - 1;
                return index;
            }

            private static Color HeightRamp(double t)
            {
                if (t <= 0.0) return Color.FromRgb(35, 93, 255);
                if (t >= 1.0) return Color.FromRgb(255, 64, 64);

                // 低->高: 蓝 -> 绿 -> 黄 -> 橙 -> 红（高度渐变，段内用 smootherstep 让过渡更自然）
                Color blue = Color.FromRgb(35, 93, 255);
                Color green = Color.FromRgb(52, 255, 106);
                Color yellow = Color.FromRgb(255, 236, 120);
                Color orange = Color.FromRgb(255, 158, 72);
                Color red = Color.FromRgb(255, 64, 64);

                if (t < 0.25) return Lerp(blue, green, SmootherStep(t / 0.25));
                if (t < 0.50) return Lerp(green, yellow, SmootherStep((t - 0.25) / 0.25));
                if (t < 0.75) return Lerp(yellow, orange, SmootherStep((t - 0.50) / 0.25));
                return Lerp(orange, red, SmootherStep((t - 0.75) / 0.25));
            }

            private static Color ApplyBrightness(Color color, double brightness01)
            {
                double brightness = 0.55 + 0.45 * Clamp01(brightness01);
                byte r = (byte)Math.Max(0, Math.Min(255, color.R * brightness));
                byte g = (byte)Math.Max(0, Math.Min(255, color.G * brightness));
                byte b = (byte)Math.Max(0, Math.Min(255, color.B * brightness));
                return Color.FromRgb(r, g, b);
            }

            private static Color Lerp(Color a, Color b, double t)
            {
                t = Clamp01(t);
                byte r = (byte)(a.R + (b.R - a.R) * t);
                byte g = (byte)(a.G + (b.G - a.G) * t);
                byte bl = (byte)(a.B + (b.B - a.B) * t);
                return Color.FromRgb(r, g, bl);
            }

            private static double SmootherStep(double t)
            {
                t = Clamp01(t);
                return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
            }

            private static double Clamp01(double value)
            {
                if (value < 0) return 0;
                if (value > 1) return 1;
                return value;
            }
        }
    }
}
