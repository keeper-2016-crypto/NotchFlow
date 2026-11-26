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
    readonly SolidColorBrush first=new(), second=new(), third=new(), accent=new();
    readonly LinearGradientBrush gradient=new();
    AppSettings settings=new();
    AlbumPalette palette=AlbumColor.Palette(null);
    ImageSource? artwork;
    bool expanded, music, moving;
    readonly float[] floorLevels=new float[64];
    readonly LinearGradientBrush floorBrush=new(Colors.White,Colors.Transparent,90);
    readonly SolidColorBrush floorColor=new();
    bool floorVisible,rendering;
    TimeSpan previousTime;
    AudioFrame audio=AudioFrame.Silent;
    public IslandSurface? Surface {get;set;}
    public double CompactMetricsWidth {get;set;}
    public bool IsAnimating => moving;
    public MediaEffects()
    {
        IsHitTestVisible=false;
        gradient.StartPoint=new Point(0,0);gradient.EndPoint=new Point(1,1);
        gradient.GradientStops.Add(new GradientStop(Colors.Gray,0));
        gradient.GradientStops.Add(new GradientStop(Colors.Gray,.5));
        gradient.GradientStops.Add(new GradientStop(Colors.Gray,1));
        foreach(var brush in new[]{first,second,third,accent})brush.Changed+=(_,_)=>InvalidateVisual();
        Unloaded+=(_,_)=>Stop();
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)Stop();};
    }
    void Stop(){moving=false;floorVisible=false;EndFloor();Array.Clear(floorLevels);audio=AudioFrame.Silent;}
    void EndFloor(){
        if(rendering)CompositionTarget.Rendering-=FloorTick;
        rendering=false;previousTime=default;
    }
    void StartFloor(){
        if(rendering||!floorVisible)return;
        rendering=true;CompositionTarget.Rendering+=FloorTick;
    }
    internal static double FloorResponse(float energy,int band,int sensitivity,bool expanded)
    {
        if(!float.IsFinite(energy)||energy<=0||sensitivity<=0)return 0;
        double bass=1+.28*Math.Exp(-band/9d);
        double drive=Math.Pow(energy,.68)*sensitivity/65d*bass*(expanded?3.0:4.1);
        return 1-Math.Exp(-drive);
    }
    void FloorTick(object? sender,EventArgs e){
        var now=((RenderingEventArgs)e).RenderingTime;
        double elapsed=previousTime==default?1d/60:(now-previousTime).TotalSeconds;
        if(elapsed<1d/65)return;
        previousTime=now;
        bool unsettled=false;
        for(int i=0;i<64;i++){
            double target=moving&&audio.Floor.Length==64?FloorResponse(audio.Floor[i],i,settings.FloorSensitivity,expanded):0;
            double delta=target-floorLevels[i];
            double tau=target>floorLevels[i]?.009+settings.FloorSmooth*.00032:.07+settings.FloorSmooth*.003;
            floorLevels[i]=Math.Abs(delta)<.001?(float)target:(float)(floorLevels[i]+delta*(1-Math.Exp(-elapsed/tau)));
            unsettled|=Math.Abs(target-floorLevels[i])>=.001;
        }
        InvalidateVisual();
        if(!unsettled)EndFloor();
    }
    public void Configure(AppSettings value, ImageSource? image, bool isExpanded, bool isMusic, bool playing)
    {
        settings=value;expanded=isExpanded;music=isMusic;
        bool animate=settings.Animations&&!settings.ReduceMotion&&SystemParameters.ClientAreaAnimation;
        if(!ReferenceEquals(artwork,image)){artwork=image;palette=AlbumColor.Palette(image);}
        Transition(first,palette.First,animate);Transition(second,palette.Second,animate);
        Transition(third,palette.Third,animate);Transition(accent,AlbumColor.Accent(value,palette),animate);
        moving=animate&&music&&IsVisible&&playing&&settings.AudioAnalysisEnabled;
        floorVisible=animate && music && IsVisible && settings.AudioAnalysisEnabled && settings.MediaVisualizer=="Spectrum Floor" && (expanded||settings.FloorCompact);
        if(floorVisible){
            var color=settings.FloorColor switch{"Album"=>palette.First,"Custom"=>(Color)ColorConverter.ConvertFromString(settings.FloorHex),_=>AlbumColor.Accent(value,palette)};
            Transition(floorColor,color,animate);
            if(!moving){audio=AudioFrame.Silent;StartFloor();}
        }else{EndFloor();Array.Clear(floorLevels);}
        if(!moving)audio=AudioFrame.Silent;
        InvalidateVisual();
    }
    public void AcceptAudio(AudioFrame frame)
    {
        if(settings.MediaVisualizer=="Spectrum Floor"){
            if(!floorVisible||!moving)return;
            audio=frame;StartFloor();return;
        }
        audio=moving?frame:AudioFrame.Silent;
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
        if(floorVisible){

            int count=settings.FloorBars==0?Math.Clamp((int)Math.Round(w/10),16,64):settings.FloorBars;
            double cell=w/count,width=cell*settings.FloorWidth/100d;
            var color=floorColor.Color;
            floorBrush.GradientStops[0].Color=Color.FromArgb(218,color.R,color.G,color.B);
            floorBrush.GradientStops[1].Color=Color.FromArgb(12,color.R,color.G,color.B);
            for(int bar=0;bar<count;bar++){
                float level=0;
                for(int i=bar*64/count;i<(bar+1)*64/count;i++)level=Math.Max(level,floorLevels[i]);
                double height=h*settings.FloorHeight/100d*level;
                if(height<.5)continue;
                var rect=new Rect(bar*cell+(cell-width)/2,h-height,width,height+width/2);
                if(settings.FloorGlow>0){
                    var halo=rect;halo.Inflate(1.5,1.5);
                    dc.PushOpacity(settings.FloorGlow*.001*settings.FloorOpacity/100);
                    dc.DrawRoundedRectangle(floorBrush,null,halo,width/2+1,width/2+1);dc.Pop();
                }
                dc.PushOpacity(settings.FloorOpacity/100d);
                dc.DrawRoundedRectangle(floorBrush,null,rect,width/2,width/2);dc.Pop();
            }
        }
        if(music && settings.MediaVisualizer=="Equalizer")
        {
            int columns=expanded?16:9, rows=expanded?6:5;
            double width=expanded?116:51,height=expanded?22:20;
            // Expanded content is 400 DIPs wide and centered in both capsule and Notch.
            // The play/pause button center is x=322; account for the final 2-DIP bar gap.
            double left=expanded?(w-400)/2+322-(width-2)/2:w-width-12-CompactMetricsWidth,top=expanded?38:(h-height)/2;
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
        dc.PushOpacity(.04+settings.BorderGlow*.08);
        dc.DrawGeometry(null,new Pen(accent,1.4),outline);
        dc.Pop();dc.Pop();
    }
}
