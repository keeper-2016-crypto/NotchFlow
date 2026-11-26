using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchFlow.Configuration;
using NotchFlow.Services;

namespace NotchFlow.Controls;

public sealed class RadialPulse : FrameworkElement
{
    readonly double[] levels=new double[64];
    readonly Pen[] strokes=new Pen[64], halos=new Pen[64];
    readonly ScaleTransform pulse=new();
    readonly Color[] previousColors=new Color[64], targetColors=new Color[64];
    readonly Stopwatch transition=new();
    double gap,width;
    Vector[] axes=Array.Empty<Vector>();
    Border cover=null!, image=null!;
    AppSettings settings=new();
    AudioFrame frame=AudioFrame.Silent;
    bool large,active,playing,listening;
    double radius,bass;
    TimeSpan previous;

    public RadialPulse()
    {
        IsHitTestVisible=false;
        for(int i=0;i<64;i++)
        {
            var brush=new SolidColorBrush(Colors.White);
            strokes[i]=new Pen(brush,1){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
            halos[i]=new Pen(brush,1){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
        }
        Unloaded+=(_,_)=>Stop();
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)Stop();};
    }

    public void Attach(Border artwork,Border artworkImage,bool expanded)
    {
        cover=artwork;image=artworkImage;large=expanded;
        cover.RenderTransformOrigin=new Point(.5,.5);cover.RenderTransform=pulse;
    }

    public void UpdateColor(AppSettings options,ImageSource? art)
    {
        if(!active || axes.Length==0)return;
        var album=AlbumColor.Palette(art);
        var accent=AlbumColor.Accent(options,album);
        Color[] colors;
        if(options.RadialColor=="Palette")
        {
            var extracted=RadialPalette.Read(art);
            if(extracted.Length==0)colors=new[]{accent};
            else
            {
                int count=options.RadialColors==0?Math.Max(3,extracted.Length):options.RadialColors;
                colors=new Color[count];
                for(int i=0;i<count;i++)
                {
                    var c=RadialPalette.Tone(extracted[i%extracted.Length],options.RadialSaturation/100);
                    colors[i]=i<extracted.Length?c:RadialPalette.Mix(c,i%2==0?Colors.White:Colors.Black,.18+.06*i);
                }
            }
        }
        else colors=new[]{options.RadialColor switch{
            "Album"=>album.First,"Custom"=>(Color)ColorConverter.ConvertFromString(options.RadialHex),_=>accent}};
        bool changed=false;
        for(int i=0;i<axes.Length;i++)
        {
            double phase=(double)i*colors.Length/axes.Length;
            Color next=options.RadialDistribution switch{
                "Gradient"=>RadialPalette.Mix(colors[(int)phase],colors[((int)phase+1)%colors.Length],phase-(int)phase),
                "Zones"=>colors[FrequencyRange(i,axes.Length,options.RadialMirror).Start*colors.Length/64],
                _=>colors[i%colors.Length]};
            changed|=targetColors[i]!=next;
            targetColors[i]=next;
        }
        if(!changed)return;
        for(int i=0;i<axes.Length;i++)
            previousColors[i]=((SolidColorBrush)strokes[i].Brush).Color;
        if(IsVisible&&!options.ReduceMotion){transition.Restart();Wake();}
        else ApplyColors(1);
    }

    void ApplyColors(double progress)
    {
        for(int i=0;i<axes.Length;i++)
            ((SolidColorBrush)strokes[i].Brush).Color=RadialPalette.Mix(previousColors[i],targetColors[i],progress);
        if(progress>=1)transition.Reset();
    }

    public void Configure(AppSettings options,ImageSource? art,bool selected,bool visible,bool playback)
    {
        settings=options;playing=playback;
        double outer=large?43:17;
        gap=outer*.065*options.RadialGap/2;
        width=outer*.054*options.RadialThickness/1.5;
        radius=Math.Min(outer*.47,outer*.81-gap-width);
        radius/=1+(options.RadialBass?options.RadialBassAmount*.001:0);
        cover.Width=cover.Height=selected?radius*2:large?74:24;
        cover.CornerRadius=image.CornerRadius=new CornerRadius(selected?radius:large?13:6);
        active=selected&&visible&&options.AudioAnalysisEnabled&&options.Animations&&!options.ReduceMotion&&SystemParameters.ClientAreaAnimation;
        if(!active){Stop();return;}
        int count=options.RadialSegments==0?(large?48:32):options.RadialSegments;
        if(count!=axes.Length){
            axes=new Vector[count];
            for(int i=0;i<count;i++){
                double angle=(i+.5)*2*Math.PI/count-Math.PI/2;
                axes[i]=new Vector(Math.Cos(angle),Math.Sin(angle));
            }
        }
        for(int i=0;i<count;i++){
            strokes[i].Thickness=width;
            halos[i].Thickness=width*1.45;
        }
        UpdateColor(options,art);
        InvalidateVisual();
        if(!playing){frame=AudioFrame.Silent;Wake();}
    }

    public void Accept(AudioFrame value)
    {
        if(!active||!playing||!IsVisible)return;
        frame=value;Wake();
    }
    void Wake()
    {
        if(listening||!IsVisible)return;
        listening=true;CompositionTarget.Rendering+=Tick;
    }
    void Sleep()
    {
        if(listening)CompositionTarget.Rendering-=Tick;
        listening=false;previous=default;
    }
    public void Stop()
    {
        if(transition.IsRunning)ApplyColors(1);
        Sleep();Array.Clear(levels);frame=AudioFrame.Silent;bass=0;
        pulse.ScaleX=pulse.ScaleY=1;InvalidateVisual();
    }
    void Tick(object? sender,EventArgs args)
    {
        var time=((RenderingEventArgs)args).RenderingTime;
        double dt=previous==default?1d/60:(time-previous).TotalSeconds;
        if(dt<1d/65)return;
        previous=time;
        bool changing=false;
        double bassTarget=0;
        for(int i=0;i<64;i++){
            float energy=frame.Floor.Length==64?frame.Floor[i]:0;
            double target=Response(energy,settings.RadialSensitivity,large);
            double delta=target-levels[i];
            double tau=(delta>0?(large?.022:.018):(large?.180:.150))*(.55+settings.RadialSmooth*.01);
            levels[i]=Math.Abs(delta)<.001?target:levels[i]+delta*(1-Math.Exp(-dt/tau));
            changing|=Math.Abs(target-levels[i])>.0001;
            if(i<7)bassTarget=Math.Max(bassTarget,levels[i]);
        }
        bass=settings.RadialBass?bassTarget:0;
        pulse.ScaleX=pulse.ScaleY=1+bass*settings.RadialBassAmount*.001;
        InvalidateVisual();
        if(transition.IsRunning)ApplyColors(Math.Min(1,transition.Elapsed.TotalMilliseconds/350));
        if(!changing&&!transition.IsRunning)Sleep();
    }
    public static double Response(float energy,double sensitivity,bool expanded)=>
        float.IsFinite(energy)&&energy>0?Math.Tanh(Math.Pow(energy,expanded?.43:.40)*sensitivity/75*(expanded?2.8:3.2)):0;

    public static (int Start,int End) FrequencyRange(int segment,int count,bool mirror)
    {
        int slots=mirror?count/2:count;
        int slot=mirror?Math.Min(segment,count-1-segment):segment;
        return (slot*64/slots,(slot+1)*64/slots);
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(!active||cover==null)return;
        Point center=new(ActualWidth/2,ActualHeight/2);
        double outer=Math.Min(center.X,center.Y)-width*.725;
        double inner=radius*(1+(settings.RadialBass?settings.RadialBassAmount*.001:0))+gap+width/2;
        double length=Math.Max(0,outer-inner)*settings.RadialLength/100;
        dc.PushOpacity(settings.RadialOpacity/100);
        for(int i=0;i<axes.Length;i++){
            var (start,end)=FrequencyRange(i,axes.Length,settings.RadialMirror);
            double level=0;for(int j=start;j<end;j++)level=Math.Max(level,levels[j]);
            if(level<.002)continue;
            dc.PushOpacity(Math.Min(1,level*8));
            Point from=center+axes[i]*inner,to=from+axes[i]*(length*level);
            if(settings.RadialGlow>0){dc.PushOpacity(settings.RadialGlow*.002);dc.DrawLine(halos[i],from,to);dc.Pop();}
            dc.DrawLine(strokes[i],from,to);dc.Pop();
        }
        dc.Pop();
    }
}
