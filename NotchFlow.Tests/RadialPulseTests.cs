using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NotchFlow.Configuration;
using NotchFlow.Controls;
using NotchFlow.Services;

internal static class RadialPulseTests
{
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static void Pump(int milliseconds){
        var loop=new DispatcherFrame();
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>{timer.Stop();loop.Continue=false;};
        timer.Start();Dispatcher.PushFrame(loop);
    }
    static IEnumerable<GeometryDrawing> Lines(Drawing drawing){
        if(drawing is GeometryDrawing line && line.Geometry is LineGeometry)yield return line;
        if(drawing is DrawingGroup group)
            foreach(var child in group.Children)
                foreach(var item in Lines(child))yield return item;
    }
    public static void Run()
    {
        foreach(int count in new[]{24,32,48,64}){
            for(int i=0;i<count;i++){
                var range=RadialPulse.FrequencyRange(i,count,true);
                Require(range==RadialPulse.FrequencyRange(count-1-i,count,true),"Mirror symmetry");
                Require(range.Start>=0&&range.End<=64&&range.Start<range.End,"FFT index bounds");
            }
            Require(Enumerable.Range(0,count).SelectMany(i=>{var (s,e)=RadialPulse.FrequencyRange(i,count,false);return Enumerable.Range(s,e-s);}).SequenceEqual(Enumerable.Range(0,64)),"Circular frequency coverage");
        }
        var frames=new List<AudioFrame>();
        foreach(double hz in new[]{100,1000,8000}){
            var analyzer=new AudioSpectrum();
            for(int i=0;i<24000;i++)analyzer.Add((float)(.09*Math.Sin(i*2*Math.PI*hz/48000)),48000,1,true);
            frames.Add(analyzer.Latest);
        }
        var peaks=frames.Select(f=>Array.IndexOf(f.Floor,f.Floor.Max())).ToArray();
        Require(peaks[0]<peaks[1]&&peaks[1]<peaks[2],"FFT tone frequency ordering");
        Require(RadialPulse.Response(.1f,75,false)>RadialPulse.Response(.1f,75,true)&&RadialPulse.Response(float.NaN,75,true)==0,"Compact gain and invalid data");
        foreach(bool expanded in new[]{false,true}){
            Require(RadialPulse.Response(.01f,75,expanded)>.36 && RadialPulse.Response(0,100,expanded)==0,"Quiet bands remain visible and silence stays silent");
            double last=0;
            foreach(double amplitude in new[]{.003,.03,.3}){
                var analyzer=new AudioSpectrum();
                for(int i=0;i<8192;i++)analyzer.Add((float)(amplitude*Math.Sin(i*2*Math.PI*1000/48000)),48000,1,true);
                float energy=analyzer.Latest.Floor.Max();
                double response=RadialPulse.Response(energy,75,expanded);
                Require(response>last && response<1 && RadialPulse.Response(energy,100,expanded)>response,"FFT amplitude and sensitivity");
                last=response;
            }
        }
        string temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try{
            var store=new SettingsStore(temp);
            var options=new AppSettings{MediaVisualizer="Radial Pulse",RadialBass=true,AudioAnalysisEnabled=true,RadialHex="#D54254",RadialColor="Custom",RadialColors=4,RadialDistribution="Zones",RadialSaturation=125};
            store.Save(options);options=store.Load();
            Require(options.RadialColors==4&&options.RadialDistribution=="Zones"&&options.RadialSaturation==125&&options.RadialBass&&options.MediaVisualizer=="Radial Pulse"&&options.RadialMirror&&options.RadialHex=="#D54254","Persisted radial settings");
            foreach(bool expanded in new[]{false,true}){
                int size=expanded?86:34;
                var grid=new Grid{Width=size,Height=size};
                var ring=new RadialPulse();var cover=new Border();var image=new Border();cover.Child=image;
                grid.Children.Add(ring);grid.Children.Add(cover);
                var window=new Window{Content=grid,SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ShowActivated=false,ShowInTaskbar=false};
                try{
                    ring.Attach(cover,image,expanded);window.Show();window.UpdateLayout();
                    ring.Configure(options,null,true,true,true);ring.Accept(frames[0]);Pump(220);
                    if(SystemParameters.ClientAreaAnimation){
                        var scale=(ScaleTransform)cover.RenderTransform;
                        Require(scale.ScaleX>1&&scale.ScaleX<=1.03&&scale.ScaleY==scale.ScaleX,"Centered bounded bass pulse");
                        var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(ring);
                        var pixels=new byte[size*size*4];bitmap.CopyPixels(pixels,size*4,0);
                        Require(pixels.Any(b=>b>0),"Rendered audio segments");
                        Require(pixels[3]==0&&pixels[(size/2*size+size/2)*4+3]==0,"Clear corners and cover center");
                        var artBytes=new byte[16*16*4];
                        foreach(var swatches in new[]{new[]{Colors.Crimson,Colors.Gold,Colors.DeepSkyBlue},new[]{Colors.Gray},new[]{Colors.Black}}){
                            for(int i=0;i<256;i++){
                                var c=swatches[(i%16)*swatches.Length/16];int offset=i*4;
                                artBytes[offset]=c.B;artBytes[offset+1]=c.G;artBytes[offset+2]=c.R;artBytes[offset+3]=255;
                            }
                            var art=BitmapSource.Create(16,16,96,96,PixelFormats.Bgra32,null,artBytes,64);
                            options.RadialColor="Palette";options.RadialColors=0;options.RadialGlow=0;
                            foreach(string mode in new[]{"Alternate","Gradient","Zones"}){
                                options.RadialDistribution=mode;
                                ring.Configure(options,art,true,true,true);
                                ring.Accept(AudioFrame.Silent with {Floor=Enumerable.Repeat(.25f,64).ToArray()});Pump(480);
                                var lines=Lines(VisualTreeHelper.GetDrawing(ring)!).ToArray();
                                Require(lines.Length==(expanded?48:32),"Default segment count");
                                var line=(LineGeometry)lines[0].Geometry;
                                Require((line.EndPoint-line.StartPoint).Length>5*lines[0].Pen.Thickness,"Elongated strokes");
                                var colors=lines.Select(l=>((SolidColorBrush)l.Pen.Brush).Color).ToArray();
                                Require(colors.Distinct().Count()>=(swatches[0]==Colors.Black?1:3),"Palette diversity");
                                if(swatches[0]==Colors.Black)Require(colors.All(c=>c==Colors.White),"Accent fallback");
                                if(mode=="Zones")Require(colors.SequenceEqual(colors.Reverse()),"Mirrored zones");
                            }
                        }
                        ring.Configure(options,null,true,true,false);Pump(1800);
                        Require(scale.ScaleX==1,"Bass settles on pause");
                    }
                    ring.Configure(options,null,false,false,false);
                    Require(cover.Width==(expanded?74:24)&&!ring.IsHitTestVisible,"Restored artwork and pointer passthrough");
                }finally{window.Close();ring.Stop();}
            }
        }finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
        Console.WriteLine("PASS: radial FFT mapping, tone response, settings, geometry, bass decay and lifecycle");
    }
}
