using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ludens
{
    /// <summary>Small motion toolkit built around FLIP (First, Last, Invert, Play).</summary>
    public static class Motion
    {
        public const int FastMs = 150;
        public const int NormalMs = 240;
        public const int SlowMs = 380;
        public const int FlipMs = 520;

        public static readonly IEasingFunction EaseOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
        public static readonly IEasingFunction EaseInOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseInOut });
        public static readonly IEasingFunction EaseIn = Freeze(new CubicEase { EasingMode = EasingMode.EaseIn });
        public static readonly IEasingFunction Emphasized = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 });

        private static IEasingFunction Freeze(IEasingFunction easing)
        {
            Freezable freezable = easing as Freezable;
            if (freezable != null) freezable.Freeze();
            return easing;
        }

        public static void FadeIn(UIElement element, double delayMs, double durationMs)
        {
            if (element == null) return;
            element.Opacity = 1;
            DoubleAnimation animation = new DoubleAnimation(0, 1, Ms(durationMs));
            animation.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            animation.EasingFunction = EaseOut;
            animation.FillBehavior = FillBehavior.Stop;
            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        public static void FadeOut(UIElement element, double durationMs, Action completed)
        {
            if (element == null)
            {
                if (completed != null) completed();
                return;
            }
            double current = element.Opacity;
            element.Opacity = 0;
            DoubleAnimation animation = new DoubleAnimation(current, 0, Ms(durationMs));
            animation.EasingFunction = EaseIn;
            animation.FillBehavior = FillBehavior.Stop;
            if (completed != null)
            {
                animation.Completed += delegate { completed(); };
            }
            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        public static void SlideFadeIn(FrameworkElement element, double dx, double dy, double delayMs, double durationMs)
        {
            if (element == null) return;
            element.Opacity = 1;
            TranslateTransform transform = EnsureTranslate(element);
            transform.X = 0;
            transform.Y = 0;
            DoubleAnimation fade = new DoubleAnimation(0, 1, Ms(durationMs));
            fade.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            fade.EasingFunction = EaseOut;
            fade.FillBehavior = FillBehavior.Stop;

            DoubleAnimation slideX = new DoubleAnimation(dx, 0, Ms(durationMs));
            slideX.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            slideX.EasingFunction = EaseOut;
            slideX.FillBehavior = FillBehavior.Stop;

            DoubleAnimation slideY = new DoubleAnimation(dy, 0, Ms(durationMs));
            slideY.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            slideY.EasingFunction = EaseOut;
            slideY.FillBehavior = FillBehavior.Stop;

            element.BeginAnimation(UIElement.OpacityProperty, fade);
            transform.BeginAnimation(TranslateTransform.XProperty, slideX);
            transform.BeginAnimation(TranslateTransform.YProperty, slideY);
        }

        public static void PopIn(FrameworkElement element, double delayMs, double durationMs)
        {
            if (element == null) return;
            element.Opacity = 1;
            ScaleTransform scale = EnsureScale(element);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            DoubleAnimation fade = new DoubleAnimation(0, 1, Ms(durationMs));
            fade.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            fade.EasingFunction = EaseOut;
            fade.FillBehavior = FillBehavior.Stop;

            DoubleAnimation grow = new DoubleAnimation(0.94, 1, Ms(durationMs));
            grow.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            grow.EasingFunction = Emphasized;
            grow.FillBehavior = FillBehavior.Stop;

            element.BeginAnimation(UIElement.OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            ScaleTransform scaleY = scale;
            DoubleAnimation growY = new DoubleAnimation(0.94, 1, Ms(durationMs));
            growY.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            growY.EasingFunction = Emphasized;
            growY.FillBehavior = FillBehavior.Stop;
            scaleY.BeginAnimation(ScaleTransform.ScaleYProperty, growY);
        }

        public static void Stagger(IList<FrameworkElement> elements, double startDelay, double step, double dx, double dy)
        {
            if (elements == null) return;
            for (int i = 0; i < elements.Count; i++)
            {
                double delay = startDelay + Math.Min(i, 12) * step;
                SlideFadeIn(elements[i], dx, dy, delay, NormalMs + 60);
            }
        }

        public static void AnimateDouble(DependencyObject target, DependencyProperty property, double from, double to,
            double durationMs, IEasingFunction easing, Action completed)
        {
            if (target == null) return;
            DoubleAnimation animation = new DoubleAnimation(from, to, Ms(durationMs));
            animation.EasingFunction = easing ?? EaseOut;
            animation.FillBehavior = FillBehavior.Stop;
            if (completed != null) animation.Completed += delegate { completed(); };
            ((UIElement)target).BeginAnimation(property, animation);
        }

        public static Rect ScreenRect(FrameworkElement element)
        {
            if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return Rect.Empty;
            try
            {
                Point origin = element.PointToScreen(new Point(0, 0));
                return new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
            }
            catch { return Rect.Empty; }
        }

        /// <summary>Animates an element from an old screen rect into its new layout position.</summary>
        public static void Flip(FrameworkElement element, Rect oldScreenRect, FrameworkElement root, Action completed)
        {
            if (element == null || oldScreenRect.IsEmpty || root == null) return;
            element.UpdateLayout();
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return;
            Point newOrigin = element.TransformToAncestor(root).Transform(new Point(0, 0));
            Rect newRect = new Rect(newOrigin, new Size(element.ActualWidth, element.ActualHeight));
            if (newRect.Width <= 0 || newRect.Height <= 0) return;

            Point oldOrigin = root.PointFromScreen(new Point(oldScreenRect.Left, oldScreenRect.Top));
            Point oldFar = root.PointFromScreen(new Point(oldScreenRect.Right, oldScreenRect.Bottom));
            Rect oldRect = new Rect(oldOrigin, oldFar);
            if (Math.Abs(newRect.Left - oldRect.Left) < 0.5 &&
                Math.Abs(newRect.Top - oldRect.Top) < 0.5 &&
                Math.Abs(newRect.Width - oldRect.Width) < 0.5 &&
                Math.Abs(newRect.Height - oldRect.Height) < 0.5)
            {
                if (completed != null) completed();
                return;
            }

            double dx = oldRect.Left - newRect.Left;
            double dy = oldRect.Top - newRect.Top;
            double sx = Math.Max(0.05, oldRect.Width / newRect.Width);
            double sy = Math.Max(0.05, oldRect.Height / newRect.Height);

            TransformGroup group = new TransformGroup();
            ScaleTransform scale = new ScaleTransform(sx, sy, 0, 0);
            TranslateTransform translate = new TranslateTransform(dx, dy);
            group.Children.Add(scale);
            group.Children.Add(translate);
            element.RenderTransformOrigin = new Point(0, 0);
            element.RenderTransform = group;

            DoubleAnimation scaleX = new DoubleAnimation(sx, 1, Ms(FlipMs));
            scaleX.EasingFunction = EaseOut;
            scaleX.FillBehavior = FillBehavior.HoldEnd;
            DoubleAnimation scaleY = new DoubleAnimation(sy, 1, Ms(FlipMs));
            scaleY.EasingFunction = EaseOut;
            scaleY.FillBehavior = FillBehavior.HoldEnd;
            DoubleAnimation moveX = new DoubleAnimation(dx, 0, Ms(FlipMs));
            moveX.EasingFunction = EaseOut;
            moveX.FillBehavior = FillBehavior.HoldEnd;
            DoubleAnimation moveY = new DoubleAnimation(dy, 0, Ms(FlipMs));
            moveY.EasingFunction = EaseOut;
            moveY.FillBehavior = FillBehavior.HoldEnd;
            moveY.Completed += delegate
            {
                element.RenderTransform = null;
                if (completed != null) completed();
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
            translate.BeginAnimation(TranslateTransform.XProperty, moveX);
            translate.BeginAnimation(TranslateTransform.YProperty, moveY);
        }

        /// <summary>Flies a cover image across the window using FLIP-style rect interpolation.</summary>
        public static Border FlyImage(ImageSource source, Rect fromScreen, Rect toScreen, Canvas layer, double cornerRadius, Action completed, int holdMs = 0)
        {
            if (source == null || layer == null || fromScreen.IsEmpty || toScreen.IsEmpty) return null;
            Rect from = ToLayerRect(fromScreen, layer);
            Rect to = ToLayerRect(toScreen, layer);
            Border border = new Border();
            border.CornerRadius = new CornerRadius(cornerRadius);
            border.ClipToBounds = true;
            border.Background = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
            border.SnapsToDevicePixels = true;
            border.IsHitTestVisible = false;
            RenderOptions.SetBitmapScalingMode(border, BitmapScalingMode.HighQuality);
            layer.Children.Add(border);
            Canvas.SetLeft(border, from.Left);
            Canvas.SetTop(border, from.Top);
            border.Width = from.Width;
            border.Height = from.Height;
            Panel.SetZIndex(border, 1000);

            Duration duration = Ms(FlipMs);
            DoubleAnimation left = new DoubleAnimation(from.Left, to.Left, duration);
            left.EasingFunction = EaseOut;
            left.FillBehavior = FillBehavior.HoldEnd;
            DoubleAnimation top = new DoubleAnimation(from.Top, to.Top, duration);
            top.EasingFunction = EaseOut;
            top.FillBehavior = FillBehavior.HoldEnd;
            DoubleAnimation width = new DoubleAnimation(from.Width, to.Width, duration);
            width.EasingFunction = EaseOut;
            width.FillBehavior = FillBehavior.HoldEnd;
            DoubleAnimation height = new DoubleAnimation(from.Height, to.Height, duration);
            height.EasingFunction = EaseOut;
            height.FillBehavior = FillBehavior.HoldEnd;

            left.Completed += delegate
            {
                border.BeginAnimation(Canvas.LeftProperty, null);
                border.BeginAnimation(Canvas.TopProperty, null);
                border.BeginAnimation(FrameworkElement.WidthProperty, null);
                border.BeginAnimation(FrameworkElement.HeightProperty, null);
                Canvas.SetLeft(border, to.Left);
                Canvas.SetTop(border, to.Top);
                border.Width = to.Width;
                border.Height = to.Height;
                if (completed != null) completed();
                if (holdMs <= 0)
                {
                    layer.Children.Remove(border);
                }
                else
                {
                    // Keep the ghost parked on the destination while the real cover fades in
                    // underneath it, so the hand-off has no blank or double-cover frame.
                    DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(holdMs));
                    fade.FillBehavior = FillBehavior.HoldEnd;
                    border.BeginAnimation(UIElement.OpacityProperty, fade);
                    DispatcherTimer hold = new DispatcherTimer();
                    hold.Interval = TimeSpan.FromMilliseconds(holdMs);
                    hold.Tick += delegate
                    {
                        hold.Stop();
                        layer.Children.Remove(border);
                    };
                    hold.Start();
                }
            };
            border.BeginAnimation(Canvas.LeftProperty, left);
            border.BeginAnimation(Canvas.TopProperty, top);
            border.BeginAnimation(FrameworkElement.WidthProperty, width);
            border.BeginAnimation(FrameworkElement.HeightProperty, height);
            return border;
        }

        public static Rect ToLayerRect(Rect screen, Canvas layer)
        {
            Point origin = layer.PointFromScreen(new Point(screen.Left, screen.Top));
            Point far = layer.PointFromScreen(new Point(screen.Right, screen.Bottom));
            return new Rect(origin, far);
        }

        public static void MoveTo(UIElement element, DependencyProperty property, double to, double durationMs, Action completed)
        {
            if (element == null) return;
            double from = 0;
            if (property == Canvas.TopProperty) from = Canvas.GetTop(element);
            else if (property == Canvas.LeftProperty) from = Canvas.GetLeft(element);
            DoubleAnimation animation = new DoubleAnimation(from, to, Ms(durationMs));
            animation.EasingFunction = EaseOut;
            animation.FillBehavior = FillBehavior.Stop;
            if (completed != null) animation.Completed += delegate { completed(); };
            element.BeginAnimation(property, animation);
        }

        private static Duration Ms(double milliseconds)
        {
            return new Duration(TimeSpan.FromMilliseconds(milliseconds));
        }

        private static TranslateTransform EnsureTranslate(FrameworkElement element)
        {
            TranslateTransform translate = element.RenderTransform as TranslateTransform;
            if (translate != null) return translate;
            TransformGroup group = element.RenderTransform as TransformGroup;
            if (group != null)
            {
                for (int i = 0; i < group.Children.Count; i++)
                {
                    translate = group.Children[i] as TranslateTransform;
                    if (translate != null) return translate;
                }
                translate = new TranslateTransform();
                group.Children.Add(translate);
                return translate;
            }
            translate = new TranslateTransform();
            element.RenderTransform = translate;
            return translate;
        }

        private static ScaleTransform EnsureScale(FrameworkElement element)
        {
            ScaleTransform scale = element.RenderTransform as ScaleTransform;
            if (scale != null && element.RenderTransformOrigin.Equals(new Point(0.5, 0.5))) return scale;
            TransformGroup group = element.RenderTransform as TransformGroup;
            if (group != null)
            {
                for (int i = 0; i < group.Children.Count; i++)
                {
                    scale = group.Children[i] as ScaleTransform;
                    if (scale != null) return scale;
                }
                scale = new ScaleTransform(1, 1, 0.5, 0.5);
                group.Children.Add(scale);
                return scale;
            }
            scale = new ScaleTransform(1, 1, 0.5, 0.5);
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = scale;
            return scale;
        }
    }
}
