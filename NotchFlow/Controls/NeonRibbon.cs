using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchFlow.Configuration;
using NotchFlow.Services;

namespace NotchFlow.Controls;

public sealed class NeonRibbon : FrameworkElement
{
    static readonly DependencyProperty PulseProperty=DependencyProperty.Register("Pulse",typeof(double),typeof(NeonRibbon),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    readonly LinearGradientBrush ribbon=new(){StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
    readonly LinearGradientBrush core=new(){StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
    AppSettings settings=new();
    bool playing,reactive,colorsInitialized;
    Geometry? outline;
    Size outlineSize;
    double radius;
    bool notch;
    public IslandSurface? Surface {get;set;}
    public double Pulse => (double)GetValue(PulseProperty);
    public NeonRibbon()
    {
        IsHitTestVisible=false;
        for(int i=0;i<3;i++){ribbon.GradientStops.Add(new GradientStop(Colors.White,i/2d));core.GradientStops.Add(new GradientStop(Colors.White,i/2d));}
        Unloaded+=(_,_)=>BeginAnimation(PulseProperty,null);
    }
    public void Configure(AppSettings value,ImageSource? artwork,bool isPlaying)
    {
        settings=value;playing=isPlaying;
        reactive=value.RibbonAnimation=="Audio Reactive"&&value.AudioAnalysisEnabled&&playing&&value.Animations&&!value.ReduceMotion&&SystemParameters.ClientAreaAnimation;
        Visibility=value.MediaVisualizer=="Neon Ribbon"&&value.RibbonAnimation!="Disabled"?Visibility.Visible:Visibility.Collapsed;
        var palette=AlbumColor.Palette(artwork);
        Color[] colors=value.RibbonColorMode switch
        {
            "Album Colors"=>new[]{palette.First,palette.Second,palette.Third},
            "Custom Color"=>new[]{(Color)ColorConverter.ConvertFromString(value.CustomAccentColor),(Color)ColorConverter.ConvertFromString(value.CustomAccentColor),(Color)ColorConverter.ConvertFromString(value.CustomAccentColor)},
            _=>new[]{Color.FromRgb(255,46,166),Color.FromRgb(168,85,247),Color.FromRgb(57,139,255)}
        };
        for(int i=0;i<3;i++)
        {
            SetColor(ribbon.GradientStops[i],colors[i]);
            var c=colors[i];
            SetColor(core.GradientStops[i],Color.FromRgb((byte)(c.R+(255-c.R)*.6),(byte)(c.G+(255-c.G)*.6),(byte)(c.B+(255-c.B)*.6)));
        }
        colorsInitialized=true;
        if(!reactive)AcceptAudio(AudioFrame.Silent);
        InvalidateVisual();
    }
    void SetColor(GradientStop stop,Color color)
    {
        if((Color)stop.GetAnimationBaseValue(GradientStop.ColorProperty)==color)return;
        var old=stop.Color;stop.BeginAnimation(GradientStop.ColorProperty,null);stop.Color=color;
        if(colorsInitialized&&settings.Animations&&!settings.ReduceMotion)stop.BeginAnimation(GradientStop.ColorProperty,new ColorAnimation(old,color,TimeSpan.FromMilliseconds(500)){FillBehavior=FillBehavior.Stop});
    }
    public static double Response(AudioFrame audio,string sensitivity)
    {
        double gain=sensitivity switch {"Low"=>.65,"High"=>1.5,_=>1};
        return Math.Clamp((audio.Bass*.5+audio.Onset*.35+audio.Mid*.1+audio.Treble*.05)*gain,0,1);
    }
    public void AcceptAudio(AudioFrame audio)
    {
        double target=reactive?Response(audio,settings.RibbonSensitivity):0;
        if(!settings.Animations||settings.ReduceMotion){BeginAnimation(PulseProperty,null);SetValue(PulseProperty,0d);return;}
        if(Math.Abs(Pulse-target)<.001)return;
        BeginAnimation(PulseProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(target>Pulse?35:240)));
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(Surface==null||ActualWidth<2||ActualHeight<2)return;
        if(outline==null||outlineSize!=RenderSize||radius!=Surface.CornerRadius.TopLeft||notch!=Surface.IsNotch)
        {
            outlineSize=RenderSize;radius=Surface.CornerRadius.TopLeft;notch=Surface.IsNotch;
            outline=Surface.Outline(ActualWidth,ActualHeight);
        }
        double intensity=settings.RibbonGlow switch {"Low"=>.55,"High"=>1,_=>.78};
        double thickness=settings.RibbonThickness switch {"Thin"=>1.5,"Bold"=>2.5,_=>2};
        double strength=intensity*(playing?1:.72)*(1+.4*Pulse);
        // Concentric low-opacity strokes approximate a Gaussian halo without a per-frame blur.
        for(int layer=12;layer>=1;layer--)
        {
            double width=thickness+layer*(1+.18*Pulse);
            dc.PushOpacity(strength*.035*Math.Exp(-layer*layer/30d));
            dc.DrawGeometry(null,new Pen(ribbon,width),outline);dc.Pop();
        }
        dc.PushOpacity(Math.Min(1,strength*.82));dc.DrawGeometry(null,new Pen(ribbon,thickness),outline);dc.Pop();
        dc.PushOpacity(Math.Min(1,strength*.9));dc.DrawGeometry(null,new Pen(core,thickness*.42),outline);dc.Pop();
    }
}
