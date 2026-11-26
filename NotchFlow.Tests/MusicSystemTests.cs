using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchFlow.Configuration;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Views;

internal static class MusicSystemTests
{
    public static void Run()
    {
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/NotchFlow;component/Themes/Dark.xaml",UriKind.Relative)});
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        using var media=new MediaService();
        var model=new IslandViewModel(media,dataDirectory:dir);
        var options=new AppSettings{MediaVisualizer="Spectrum Floor",SystemMonitorEnabled=true,Animations=false,FileShelfEnabled=false};
        var window=new IslandWindow(model,options);
        try{
            window.Show();
            var capsule=(FrameworkElement)window.FindName("Capsule");
            var track=(StackPanel)window.FindName("CompactTrack");
            var metrics=(StackPanel)window.FindName("MusicMetrics");
            double Snapshot(string title,string artist){
                model.AcceptSnapshot(new(title,artist,null,true,true,true,true,true,TimeSpan.Zero,TimeSpan.FromMinutes(3),DateTimeOffset.UtcNow,1));
                window.UpdateLayout();
                double center=capsule.TranslatePoint(new Point(capsule.ActualWidth/2,0),window).X;
                if(Math.Abs(center-window.ActualWidth/2)>1)throw new InvalidOperationException("Island center moved");
                if(metrics.IsVisible && track.TranslatePoint(new Point(track.ActualWidth,0),window).X>metrics.TranslatePoint(new Point(),window).X)
                    throw new InvalidOperationException("Track and metrics overlap");
                return capsule.ActualWidth;
            }
            foreach(var mode in new[]{"Classic","Equalizer","Spectrum Floor","Radial Pulse"}){
                options.MediaVisualizer=mode;window.Apply(options);
                double shortWidth=Snapshot("A","B");
                double artistWidth=Snapshot("A","Длинное имя исполнителя");
                double longWidth=Snapshot(new string('W',100),"B");
                if(!(shortWidth<artistWidth && artistWidth<longWidth && longWidth==options.MaximumMusicWidth))
                    throw new InvalidOperationException("Adaptive text limits "+mode);
                var value=(TextBlock)window.FindName("MiniCpuValue");
                value.Text="100%";window.UpdateLayout();
                if(capsule.ActualWidth!=longWidth)throw new InvalidOperationException("Unstable metric slot");
                options.SystemMonitorEnabled=false;window.Apply(options);
                if(Snapshot("A","B")>=shortWidth)throw new InvalidOperationException("Disabled metric width");
                options.SystemMonitorEnabled=true;
            }
            options.AdaptiveMusicWidth=false;window.Apply(options);
            if(Snapshot("A","B")!=Snapshot(new string('W',100),"B"))throw new InvalidOperationException("Fixed mode");
            options.AdaptiveMusicWidth=true;window.Apply(options);
            Snapshot("A","B");
            if(SystemParameters.ClientAreaAnimation){
                options.Animations=true;window.Apply(options);
                void Pump(int ms){
                    var frame=new System.Windows.Threading.DispatcherFrame();
                    var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};
                    timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                double start=capsule.ActualWidth;
                Snapshot(new string('W',100),"B"); Pump(90);
                if(capsule.ActualWidth<=start || capsule.ActualWidth>=options.MaximumMusicWidth)throw new InvalidOperationException("Intermediate animated width");
                Snapshot("A","B");Pump(320);
                if(Math.Abs(capsule.ActualWidth-start)>1)throw new InvalidOperationException("Retargeted width");
                options.Animations=false;window.Apply(options);
            }
            window.Reveal();window.UpdateLayout();
            if(!((FrameworkElement)window.FindName("PlayerContent")).IsVisible||((FrameworkElement)window.FindName("MusicMetrics")).IsVisible)
                throw new InvalidOperationException("Expanded music layout");
            Console.WriteLine("PASS: short and long compact titles, separate metric zone, expanded player");
        }finally{window.Close();Directory.Delete(dir,true);}
    }
}
