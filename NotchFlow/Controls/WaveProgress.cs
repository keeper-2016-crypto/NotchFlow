using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace NotchFlow.Controls;

// Decorative progress wave; it does not sample or record audio.
public sealed class WaveProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(WaveProgress), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(WaveProgress), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    static readonly DependencyProperty PhaseProperty = DependencyProperty.Register("Phase", typeof(double), typeof(WaveProgress), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public bool Moving { get; private set; }
    public void SetMotion(bool enabled)
    {
        if (Moving == enabled) return;
        Moving = enabled;
        BeginAnimation(PhaseProperty, enabled ? new DoubleAnimation(0, Math.PI * 2, TimeSpan.FromSeconds(1.4)) { RepeatBehavior = RepeatBehavior.Forever } : null);
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = Math.Max(0, ActualWidth - 4), center = ActualHeight / 2, head = width * Math.Clamp(Value / 100, 0, 1);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var track = new Pen(new SolidColorBrush(Color.FromRgb(55,55,57)), 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(track, new Point(head + 2, center), new Point(width + 2, center));
        if (head > 0)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(2, center), false, false);
                for (double x = 0; x < head; x += 2) g.LineTo(new Point(x + 2, center + (Moving ? Math.Sin(x / 3.2 - (double)GetValue(PhaseProperty)) * 2.6 : 0)), true, false);
                g.LineTo(new Point(head + 2, center), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, new Pen(Accent, 2.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
        }
        dc.DrawRoundedRectangle(Brushes.White, null, new Rect(head, center - 5, 3, 10), 1.5, 1.5);
    }
}
