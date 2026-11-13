using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchFlow.Configuration;
using NotchFlow.Services;

namespace NotchFlow.Controls;

// Audio-reactive decoration; layout and input geometry stay fixed.
public sealed class MediaEffects : FrameworkElement
{
    static readonly DependencyProperty EnergyProperty=DependencyProperty.Register("Energy",typeof(double),typeof(MediaEffects),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    readonly SolidColorBrush first=new(), second=new(), third=new(), accent=new();
    readonly LinearGradientBrush gradient=new();
    readonly RadialGradientBrush glow=new();
    AppSettings settings=new();
    AlbumPalette palette=AlbumColor.Palette(null);
    ImageSource? artwork;
    bool expanded, music, moving;
    AudioFrame audio=AudioFrame.Silent;
    public IslandSurface? Surface {get;set;}
    public bool IsAnimating => moving;
    public MediaEffects()
    {
        IsHitTestVisible=false;
        gradient.StartPoint=new Point(0,0);gradient.EndPoint=new Point(1,1);
        gradient.GradientStops.Add(new GradientStop(Colors.Gray,0));
        gradient.GradientStops.Add(new GradientStop(Colors.Gray,.5));
        gradient.GradientStops.Add(new GradientStop(Colors.Gray,1));
        glow.GradientStops.Add(new GradientStop(Colors.Gray,0));
        glow.GradientStops.Add(new GradientStop(Colors.Transparent,1));
        foreach(var brush in new[]{first,second,third,accent})brush.Changed+=(_,_)=>InvalidateVisual();
        Unloaded+=(_,_)=>Stop();
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)Stop();};
    }
    void Stop(){moving=false;BeginAnimation(EnergyProperty,null);SetValue(EnergyProperty,0d);}
    public void Configure(AppSettings value, ImageSource? image, bool isExpanded, bool isMusic, bool playing)
    {
        settings=value;expanded=isExpanded;music=isMusic;
        bool animate=settings.Animations&&!settings.ReduceMotion&&SystemParameters.ClientAreaAnimation;
        if(!ReferenceEquals(artwork,image)){artwork=image;palette=AlbumColor.Palette(image);}
        Transition(first,palette.First,animate);Transition(second,palette.Second,animate);
        Transition(third,palette.Third,animate);Transition(accent,AlbumColor.Accent(value,palette),animate);
        moving=animate&&music&&IsVisible&&playing&&settings.AudioAnalysisEnabled;
        if(!moving)Stop();
        InvalidateVisual();
    }
    public void AcceptAudio(AudioFrame frame)
    {
        audio=moving?frame:AudioFrame.Silent;
        double target=moving?Math.Max(audio.Bass,Math.Max(audio.Onset,audio.Rms*.35)):0;
        BeginAnimation(EnergyProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(target>(double)GetValue(EnergyProperty)?65:220)));
        InvalidateVisual();
    }
    static void Transition(SolidColorBrush brush,Color color,bool animate)
    {
        if((Color)brush.GetAnimationBaseValue(SolidColorBrush.ColorProperty)==color)return;
        var previous=brush.Color;brush.BeginAnimation(SolidColorBrush.ColorProperty,null);brush.Color=color;
        if(animate)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(previous,color,TimeSpan.FromMilliseconds(550)){FillBehavior=FillBehavior.Stop});
    }
    protected override void OnRender(DrawingContext dc)
    {
        double w=ActualWidth,h=ActualHeight;
        if(w<2||h<2||Surface==null)return;
        var outline=Surface.Outline(w,h);
        dc.PushClip(outline);
        gradient.GradientStops[0].Color=first.Color;gradient.GradientStops[1].Color=second.Color;gradient.GradientStops[2].Color=third.Color;
        double shade=.07+(1-settings.TintIntensity)*.12;
        if(settings.BackgroundEffect=="Album Gradient" || music && expanded && settings.AlbumArtStyle is "Gradient" or "Blur + Gradient")
        {
            dc.PushOpacity(shade);dc.DrawRectangle(gradient,null,new Rect(0,0,w,h));dc.Pop();
        }
        if(music && expanded && settings.AlbumArtStyle is "Blurred" or "Blur + Gradient" && palette.SoftArtwork!=null)
        {
            dc.PushOpacity(.1);dc.DrawImage(palette.SoftArtwork,new Rect(0,0,w,h));dc.Pop();
        }
        double energy=(double)GetValue(EnergyProperty);
        double intensity=settings.AnimationIntensity switch {"Low"=>.5,"High"=>1,_=>.75};
        double pulse=energy*intensity;
        if(music && settings.MediaVisualizer=="Album Glow")
        {
            glow.GradientStops[0].Color=first.Color;
            dc.PushOpacity(.08+.38*pulse);
            dc.DrawEllipse(glow,null,expanded?new Point(w-35,20):new Point(w-15,h/2),expanded?150:85,expanded?65:35);
            glow.GradientStops[0].Color=third.Color;
            if(expanded)dc.DrawEllipse(glow,null,new Point(w,h/2),75,h);
            dc.Pop();
        }
        if(music && settings.MediaVisualizer=="Breathing Island")
        {
            dc.PushOpacity(.12+.6*pulse);
            double inset=2+(1-pulse)*1.5;
            dc.PushTransform(new TranslateTransform(inset,inset));
            dc.DrawGeometry(null,new Pen(accent,1+intensity*pulse),Surface.Outline(Math.Max(0,w-2*inset),Math.Max(0,h-2*inset)));
            dc.Pop();dc.Pop();
        }
        if(music && settings.MediaVisualizer=="Equalizer")
        {
            int columns=expanded?16:9, rows=expanded?6:5;
            double width=expanded?116:51,height=expanded?22:20;
            double left=expanded?258:w-width-12,top=expanded?38:(h-height)/2;
            double step=width/columns, rowHeight=height/rows;
            for(int column=0;column<columns;column++)
            {
                double level=moving?audio.Bands[column*16/columns]:0;
                for(int row=0;row<rows;row++)
                {
                    dc.PushOpacity(row<level*rows?.95:.025);
                    dc.DrawRoundedRectangle(accent,null,new Rect(left+column*step,top+height-(row+1)*rowHeight,step-2,rowHeight-1),.6,.6);
                    dc.Pop();
                }
            }
        }
        dc.PushOpacity(settings.MediaVisualizer=="Breathing Island"?0:.04+settings.BorderGlow*.08);
        dc.DrawGeometry(null,new Pen(accent,1.4),outline);
        dc.Pop();dc.Pop();
    }
}
