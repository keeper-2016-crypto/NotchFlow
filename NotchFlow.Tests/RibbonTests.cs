using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchFlow.Configuration;
using NotchFlow.Controls;
using NotchFlow.Services;

internal static class RibbonTests
{
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    public static void Run(string root)
    {
        var settings=new AppSettings{MediaVisualizer="Neon Ribbon",RibbonColorMode="Album Colors",RibbonGlow="High",RibbonSensitivity="High",RibbonThickness="Bold",RibbonAnimation="Static"};
        var store=new SettingsStore(System.IO.Path.Combine(root,"ribbon"));
        store.Save(settings);var loaded=store.Load();
        Check(loaded.MediaVisualizer=="Neon Ribbon"&&loaded.RibbonAnimation=="Static"&&loaded.RibbonColorMode=="Album Colors"&&loaded.RibbonThickness=="Bold","Ribbon settings round trip");
        Check(NeonRibbon.Response(AudioFrame.Silent,"High")==0,"Ribbon silence has no impulse");
        var beat=new AudioFrame(.7f,.8f,.3f,.1f,1,.9f,new float[16]);
        Check(NeonRibbon.Response(beat,"High")>NeonRibbon.Response(beat,"Low"),"Ribbon sensitivity controls real audio response");
        var directory=System.IO.Path.Combine(Environment.CurrentDirectory,".build","ribbon-screenshots");
        System.IO.Directory.CreateDirectory(directory);
        foreach(bool notch in new[]{false,true})
        foreach(bool expanded in new[]{false,true})
        {
            double width=expanded?400:212,height=expanded?150:36;
            var surface=new IslandSurface{IsNotch=notch,CornerRadius=new CornerRadius(expanded?36:18)};
            var ribbon=new NeonRibbon{Surface=surface,Width=width,Height=height};
            ribbon.Configure(new AppSettings{MediaVisualizer="Neon Ribbon",RibbonAnimation="Static",Animations=false},null,false);
            ribbon.Measure(new Size(width,height));ribbon.Arrange(new Rect(0,0,width,height));
            foreach(double scale in new[]{1d,1.25,1.5,2d})
            {
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,width+24,height+24));
                    dc.DrawRectangle(new VisualBrush(ribbon){Stretch=Stretch.None},null,new Rect(12,12,width,height));
                }
                var bitmap=new RenderTargetBitmap((int)((width+24)*scale),(int)((height+24)*scale),96*scale,96*scale,PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file=System.IO.File.Create(System.IO.Path.Combine(directory,$"{notch}-{expanded}-{scale}.png"));
                encoder.Save(file);
            }
            Check(!ribbon.IsHitTestVisible,"Ribbon does not capture input");
            ribbon.Configure(new AppSettings{MediaVisualizer="Neon Ribbon",RibbonAnimation="Disabled"},null,true);
            Check(ribbon.Visibility==Visibility.Collapsed,"Disabled ribbon is hidden");
        }
    }
}
