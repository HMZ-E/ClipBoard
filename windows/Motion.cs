using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClipBoardWindows
{
    internal enum MotionLevel { Off, Reduced, Full }
    internal sealed class MotionParts
    {
        internal readonly ScaleTransform EntryScale = new ScaleTransform(1, 1);
        internal readonly ScaleTransform DepthScale = new ScaleTransform(1, 1);
        internal readonly ScaleTransform HoverScale = new ScaleTransform(1, 1);
        internal readonly TranslateTransform EntryOffset = new TranslateTransform();
        internal readonly TranslateTransform DepthOffset = new TranslateTransform();
    }
    internal static class Motion
    {
        static readonly DependencyProperty PartsProperty = DependencyProperty.RegisterAttached("Parts", typeof(MotionParts), typeof(Motion));
        internal static MotionLevel Level(string setting)
        {
            return setting == "Off" ? MotionLevel.Off : setting == "Reduced" ? MotionLevel.Reduced : MotionLevel.Full;
        }
        internal static MotionParts Parts(FrameworkElement element)
        {
            var parts = (MotionParts)element.GetValue(PartsProperty);
            if (parts != null) return parts;
            parts = new MotionParts(); var group = new TransformGroup();
            group.Children.Add(parts.EntryScale); group.Children.Add(parts.DepthScale); group.Children.Add(parts.HoverScale);
            group.Children.Add(parts.EntryOffset); group.Children.Add(parts.DepthOffset);
            element.RenderTransform = group; element.RenderTransformOrigin = new Point(.5, .5); element.SetValue(PartsProperty, parts);
            return parts;
        }
        internal static void To(Animatable target, DependencyProperty property, double value, int milliseconds, double? from = null, int delay = 0, Action completed = null)
        {
            double start = from ?? (double)target.GetValue(property);
            target.SetValue(property, value);
            var animation = Timeline(start, value, milliseconds, delay);
            if (completed != null) animation.Completed += delegate { completed(); };
            target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }
        // UIElement is not Animatable, so fades use its own animation API.
        internal static void Fade(FrameworkElement element, double to, int duration, double? from = null, int delay = 0, Action completed = null)
        {
            double start = from ?? element.Opacity; element.Opacity = to;
            var animation = Timeline(start, to, duration, delay);
            if (completed != null) animation.Completed += delegate { completed(); };
            element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
        static DoubleAnimationUsingKeyFrames Timeline(double from, double to, int duration, int delay)
        {
            var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (delay > 0) animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + duration)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            return animation;
        }
        internal static void Enter(FrameworkElement element, MotionLevel level, int delay = 0, bool panel = false)
        {
            Reset(element); if (level == MotionLevel.Off) return;
            var parts = Parts(element);
            Fade(element, 1, panel ? 280 : 300, 0, delay);
            if (level != MotionLevel.Full) return;
            To(parts.EntryOffset, TranslateTransform.YProperty, 0, panel ? 360 : 380, panel ? 30 : 22, delay);
            To(parts.EntryScale, ScaleTransform.ScaleXProperty, 1, 360, panel ? .94 : .97, delay);
            To(parts.EntryScale, ScaleTransform.ScaleYProperty, 1, 360, panel ? .94 : .97, delay);
        }
        internal static void Exit(FrameworkElement element, MotionLevel level, Action completed)
        {
            if (level == MotionLevel.Off) { completed(); return; }
            if (level == MotionLevel.Full) {
                var parts = Parts(element);
                To(parts.EntryOffset, TranslateTransform.YProperty, 16, 150);
                To(parts.EntryScale, ScaleTransform.ScaleXProperty, .96, 150);
                To(parts.EntryScale, ScaleTransform.ScaleYProperty, .96, 150);
            }
            Fade(element, 0, 150, null, 0, completed);
        }
        internal static void Hover(FrameworkElement element, MotionLevel level, bool over)
        {
            var parts = Parts(element);
            double scale = over && level == MotionLevel.Full ? 1.025 : 1;
            if (level == MotionLevel.Off) { parts.HoverScale.ScaleX = scale; parts.HoverScale.ScaleY = scale; return; }
            To(parts.HoverScale, ScaleTransform.ScaleXProperty, scale, 170);
            To(parts.HoverScale, ScaleTransform.ScaleYProperty, scale, 170);
        }
        internal static void ColorTo(GradientStop stop, Color color, MotionLevel level)
        {
            Color before = stop.Color; stop.Color = color;
            stop.BeginAnimation(GradientStop.ColorProperty, level == MotionLevel.Off ? null : new ColorAnimation(before, color, TimeSpan.FromMilliseconds(650)) { FillBehavior = FillBehavior.Stop });
        }
        internal static void Reset(FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null); element.Opacity = 1;
            var parts = Parts(element);
            foreach (var scale in new[] { parts.EntryScale, parts.DepthScale, parts.HoverScale }) {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = 1;
            }
            foreach (var offset in new[] { parts.EntryOffset, parts.DepthOffset }) {
                offset.BeginAnimation(TranslateTransform.XProperty, null); offset.BeginAnimation(TranslateTransform.YProperty, null); offset.X = offset.Y = 0;
            }
        }
        internal static void Drift(TranslateTransform offset, bool running, bool reverse)
        {
            offset.BeginAnimation(TranslateTransform.XProperty, null); offset.BeginAnimation(TranslateTransform.YProperty, null);
            offset.X = offset.Y = 0; if (!running) return;
            offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(reverse ? 65 : -65, reverse ? -65 : 65, TimeSpan.FromSeconds(reverse ? 7 : 5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(reverse ? -40 : 30, reverse ? 40 : -30, TimeSpan.FromSeconds(reverse ? 6 : 8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }
    }
}
