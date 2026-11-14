using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchFlow.Configuration;
using NotchFlow.Services;

namespace NotchFlow.Controls;

// Foreground material and fallback; NativeBackdrop supplies desktop blur separately.
public sealed class LiquidGlassSurface : FrameworkElement
{
    static readonly DependencyProperty HoverProperty=DependencyProperty.Register("Hover",typeof(double),typeof(LiquidGlassSurface),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    static readonly DependencyProperty PressProperty=DependencyProperty.Register("Press",typeof(double),typeof(LiquidGlassSurface),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    readonly SolidColorBrush tint=new();
    readonly LinearGradientBrush lighting=new();
    readonly LinearGradientBrush edge=new();
    readonly LinearGradientBrush innerEdge=new();
    readonly RadialGradientBrush reflection=new();
    AppSettings settings=new();
    Geometry? shape;
    Size shapeSize;
    double radius;
    bool notch;
    public IslandSurface? Surface {get;set;}
    public LiquidGlassSurface()
    {
        IsHitTestVisible=false;
        lighting.StartPoint=new Point(0,0);lighting.EndPoint=new Point(.3,1);
        lighting.GradientStops.Add(new GradientStop(Color.FromArgb(22,235,239,250),0));
        lighting.GradientStops.Add(new GradientStop(Color.FromArgb(2,180,185,200),.4));
        lighting.GradientStops.Add(new GradientStop(Color.FromArgb(35,0,0,0),1));
        edge.StartPoint=new Point(.15,0);edge.EndPoint=new Point(.8,1);
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(110,240,244,255),0));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(30,160,170,190),.45));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(140,0,0,0),.8));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(42,220,228,245),1));
        innerEdge.StartPoint=new Point(0,0);innerEdge.EndPoint=new Point(0,1);
        innerEdge.GradientStops.Add(new GradientStop(Color.FromArgb(18,255,255,255),0));
        innerEdge.GradientStops.Add(new GradientStop(Color.FromArgb(55,0,0,0),1));
        reflection.Center=reflection.GradientOrigin=new Point(.25,0);
        reflection.RadiusX=.8;reflection.RadiusY=.7;
        reflection.GradientStops.Add(new GradientStop(Color.FromArgb(32,232,238,255),0));
        reflection.GradientStops.Add(new GradientStop(Colors.Transparent,1));
        Unloaded+=(_,_)=>ResetMotion();
    }
    public void Configure(AppSettings value,ImageSource? artwork)
    {
        settings=value;
        Visibility=value.BackgroundEffect=="Liquid Glass"?Visibility.Visible:Visibility.Collapsed;
        Color color=value.GlassTint switch
        {
            "Neutral"=>Color.FromRgb(26,26,32),
            "Album Color"=>Darken(AlbumColor.Palette(artwork).First),
            "Custom"=>(Color)ColorConverter.ConvertFromString(value.GlassCustomTint),
            _=>Color.FromRgb(11,11,13)
        };
        if(tint.Color!=color)tint.Color=color;
        tint.Opacity=value.GlassOpacity;
        if(!value.GlassDynamicLighting||value.ReduceMotion||!value.Animations)ResetMotion();
        InvalidateVisual();
    }
    static Color Darken(Color value)=>Color.FromRgb((byte)(11+value.R*.07),(byte)(11+value.G*.07),(byte)(13+value.B*.07));
    void ResetMotion(){BeginAnimation(HoverProperty,null);BeginAnimation(PressProperty,null);SetValue(HoverProperty,0d);SetValue(PressProperty,0d);}
    public void Hover(bool entered)
    {
        if(Visibility!=Visibility.Visible||!settings.GlassDynamicLighting||settings.ReduceMotion||!settings.Animations||!SystemParameters.ClientAreaAnimation)return;
        BeginAnimation(HoverProperty,new DoubleAnimation(entered?1:0,TimeSpan.FromMilliseconds(180)));
    }
    public void Press()
    {
        if(Visibility!=Visibility.Visible||!settings.GlassDynamicLighting||settings.ReduceMotion||!settings.Animations||!SystemParameters.ClientAreaAnimation)return;
        BeginAnimation(PressProperty,new DoubleAnimation(.6,0,TimeSpan.FromMilliseconds(220)));
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(Surface==null||ActualWidth<2||ActualHeight<2)return;
        if(shape==null||shapeSize!=RenderSize||radius!=Surface.CornerRadius.TopLeft||notch!=Surface.IsNotch)
        {
            shapeSize=RenderSize;radius=Surface.CornerRadius.TopLeft;notch=Surface.IsNotch;
            shape=Surface.Outline(ActualWidth,ActualHeight);
        }
        dc.PushClip(shape);
        dc.DrawGeometry(tint,null,shape);
        double strength=settings.GlassReflection switch {"Off"=>0,"Low"=>.4,"High"=>1,_=>.65};
        double hover=(double)GetValue(HoverProperty),press=(double)GetValue(PressProperty);
        dc.PushOpacity(strength*(.8+.15*hover+.05*press));
        dc.DrawGeometry(lighting,null,shape);
        dc.DrawGeometry(reflection,null,shape);
        dc.Pop();
        if(settings.GlassEdgeHighlight)
        {
            dc.PushOpacity(.7+.2*hover+.1*press);
            dc.DrawGeometry(null,new Pen(edge,1.4),shape);
            dc.PushTransform(new TranslateTransform(1.4,1.4));
            dc.DrawGeometry(null,new Pen(innerEdge,.7),Surface.Outline(Math.Max(0,ActualWidth-2.8),Math.Max(0,ActualHeight-2.8)));
            dc.Pop();dc.Pop();
        }
        dc.Pop();
    }
}
