using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NotchFlow.Configuration;
using NotchFlow.Controls;
using NotchFlow.Models;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Views;

internal static class VisualTests
{
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
    public static void Pump(int milliseconds)
    {
        var frame=new DispatcherFrame();
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
        timer.Start();Dispatcher.PushFrame(frame);
    }
    public static void Run(string root)
    {
        var store=new SettingsStore(System.IO.Path.Combine(root,"visual-settings"));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(store.FilePath)!);
        System.IO.File.WriteAllText(store.FilePath,"{\"Scale\":1.2,\"WeatherCity\":\"Москва\"}");
        var legacy=store.Load();Check(legacy.Scale==1.2&&legacy.MediaVisualizer=="Classic"&&legacy.AlbumArtStyle=="Clean","Legacy settings preserve data and receive safe visual defaults");
        var value=new AppSettings{MediaVisualizer="Equalizer",AccentMode="Custom HEX",CustomAccentColor="#22D3EE",AlbumArtStyle="Blur + Gradient",BackgroundEffect="Liquid Glass",ReduceMotion=true};
        value.Validate();store.Save(value);var loaded=store.Load();
        Check(loaded.MediaVisualizer==value.MediaVisualizer&&loaded.CustomAccentColor==value.CustomAccentColor&&loaded.ReduceMotion&&loaded.BackgroundEffect==value.BackgroundEffect,"Visual settings persist");
        bool rejected=false;try{new AppSettings{CustomAccentColor="#XYZ"}.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid custom HEX rejected");
        var locks=new PopupInteractionManager();object slider=new(),popup=new();
        locks.Set(slider,true);locks.Set(slider,true);locks.Set(popup,true);locks.Set(slider,false);
        Check(locks.IsActive,"Ending slider interaction keeps popup lock");locks.Set(popup,false);locks.Set(slider,false);Check(!locks.IsActive,"Duplicate releases leave no stale lock");
        byte[] pixels=new byte[32*32*4];
        for(int i=0;i<pixels.Length;i+=4){pixels[i]=80;pixels[i+1]=(byte)(i%220);pixels[i+2]=200;pixels[i+3]=255;}
        var art=BitmapSource.Create(32,32,96,96,PixelFormats.Bgra32,null,pixels,128);art.Freeze();
        Check(ReferenceEquals(AlbumColor.Palette(art),AlbumColor.Palette(art))&&AlbumColor.Palette(art).SoftArtwork!=null,"Artwork palette and soft image are cached");
        using var media=new MediaService();
        var vm=new IslandViewModel(media,dataDirectory:System.IO.Path.Combine(root,"visual-data"));
        var snapshot=new MediaSnapshot("Test track","Test artist",art,true,true,true,true,true,TimeSpan.FromSeconds(40),TimeSpan.FromSeconds(240),DateTimeOffset.UtcNow,1,"test","1");
        vm.AcceptSnapshot(snapshot);
        var window=new IslandWindow(vm,new AppSettings{Animations=false,CollapseOnLeave=false});
        window.Show();window.Reveal();Pump(60);
        var effects=(MediaEffects)window.FindName("EffectsLayer");
        var surface=(IslandSurface)window.FindName("Capsule");
        int count=0;
        foreach(var visualizer in new[]{"Classic","Album Glow","Breathing Island","Equalizer"})
        foreach(var background in new[]{"Solid","Acrylic Glass","Liquid Glass","Album Gradient"})
        foreach(var style in new[]{"Clean","Blurred","Gradient","Blur + Gradient"})
        {
            var settings=new AppSettings{MediaVisualizer=visualizer,BackgroundEffect=background,AlbumArtStyle=style,AccentMode="Dynamic Album Colors",Animations=false,CollapseOnLeave=false};
            settings.Validate();window.Apply(settings);window.UpdateLayout();
            var bitmap=new RenderTargetBitmap(440,432,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
            Check(surface.ActualWidth==400&&surface.ActualHeight==150&&window.Opacity==1,"Stable visual combination "+(++count));
        }
        foreach(double scale in new[]{1d,1.25,1.5,2d})
        {
            var bitmap=new RenderTargetBitmap((int)(440*scale),(int)(432*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(window);
        }
        Check(true,"Rendering at 100/125/150/200 percent");
        window.Apply(new AppSettings{MediaVisualizer="Equalizer",Animations=true,CollapseOnLeave=false});
        Pump(80);Check(!effects.IsAnimating,"Audio analysis off stays static");
        vm.AcceptSnapshot(snapshot with {Playing=false});Pump(600);Check(!effects.IsAnimating,"Pause stops continuous visual animation");
        window.Apply(new AppSettings{MediaVisualizer="Equalizer",ReduceMotion=true,CollapseOnLeave=false});
        vm.AcceptSnapshot(snapshot);Check(!effects.IsAnimating,"Reduce Motion prevents animation");
        window.ConfirmDeletion("Delete task?",()=>throw new InvalidOperationException("Cancel invoked deletion"));
        Check(window.HasInteractiveOverlay,"Confirmation holds interaction lock");
        var host=(Grid)window.Content;
        var overlay=(Grid)host.Children[host.Children.Count-1];
        var panel=(StackPanel)((Border)overlay.Children[0]).Child;
        var cancel=(Button)((StackPanel)panel.Children[2]).Children[0];
        cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!window.HasInteractiveOverlay,"Cancel releases confirmation lock without deleting");
        vm.AcceptSnapshot(null);window.Apply(new AppSettings{MediaVisualizer="Equalizer",AnimateOnlyWhilePlaying=false,CollapseOnLeave=false});
        Check(!effects.IsAnimating,"No media session stops visual animation even with idle animation enabled");
        vm.AcceptSnapshot(snapshot);
        var sliderControl=(Slider)window.FindName("SystemVolume");
        sliderControl.IsEnabled=true;
        window.Apply(new AppSettings{Animations=false,CollapseOnLeave=true});
        window.Activate();sliderControl.Focus();
        Check(Mouse.Capture(sliderControl,CaptureMode.SubTree),"Volume slider captures mouse");
        Pump(450);
        Check(window.HasInteractiveOverlay&&window.IsExpanded,"Captured volume slider keeps island expanded");
        window.ShowVolume(60,false);
        Check(((Grid)window.FindName("Expanded")).Visibility==Visibility.Visible,"Volume callback preserves player during drag");
        window.ConfirmDeletion("Delete task?",()=>{});
        Mouse.Capture(null);Pump(30);
        Check(window.HasInteractiveOverlay,"Ending volume capture preserves confirmation lock");
        host=(Grid)window.Content;overlay=(Grid)host.Children[host.Children.Count-1];
        panel=(StackPanel)((Border)overlay.Children[0]).Child;
        ((Button)((StackPanel)panel.Children[2]).Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!window.HasInteractiveOverlay,"Last interaction release restores normal collapse");
        var setExpanded=typeof(IslandWindow).GetMethod("SetExpanded",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        var screenshotDirectory=System.IO.Path.Combine(Environment.CurrentDirectory,".build","visual-screenshots");
        System.IO.Directory.CreateDirectory(screenshotDirectory);
        foreach(var mode in new[]{"Classic","Album Glow","Breathing Island","Equalizer","Neon Ribbon"})
        {
            var styleSettings=new AppSettings{MediaVisualizer=mode,BackgroundEffect="Album Gradient",AlbumArtStyle="Blur + Gradient",AccentMode="Dynamic Album Colors",CollapseOnLeave=false};
            window.Apply(styleSettings);
            foreach(bool isExpanded in new[]{false,true})
            {
                setExpanded.Invoke(window,new object[]{isExpanded,"visual-test"});Pump(450);
                var reactive=styleSettings.Clone();reactive.AudioAnalysisEnabled=true;
                effects.Configure(reactive,art,isExpanded,true,true);
                effects.AcceptAudio(new AudioFrame(.5f,.7f,.4f,.2f,.8f,.6f,new float[]{.2f,.4f,.8f,.6f,.3f,.2f,.4f,.5f,.3f,.6f,.9f,.5f,.3f,.4f,.2f,.1f}));
                Pump(100);
                var bitmap=new RenderTargetBitmap(440,432,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file=System.IO.File.Create(System.IO.Path.Combine(screenshotDirectory,mode.Replace(" ","-")+(isExpanded?"-expanded":"-compact")+".png"));
                encoder.Save(file);
                Check(window.IsExpanded==isExpanded,"Visualizer geometry "+mode+" expanded="+isExpanded);
            }
        }
        foreach(var mode in new[]{"Default White","Red","Purple","Blue","Cyan","Green","Pink","Custom HEX","Dynamic Album Colors"})
        {
            var settings=new AppSettings{AccentMode=mode,CustomAccentColor="#123456"};
            settings.Validate();Check(AlbumColor.Accent(settings,AlbumColor.Palette(art)).A==255,"Opaque accent "+mode);
        }
        window.Apply(new AppSettings{Animations=false,CollapseOnLeave=false});
        sliderControl.IsEnabled=true;
        for(int drag=0;drag<6;drag++)
        {
            sliderControl.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});
            Check(window.HasInteractiveOverlay&&sliderControl.IsMouseCaptured,"Track click captures full slider");
            window.ShowVolume(20+drag,false);
            sliderControl.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});
            Pump(10);
            Check(!window.HasInteractiveOverlay&&!sliderControl.IsMouseCaptured&&window.IsExpanded,"Repeated drag release leaves player open");
        }
        window.ShowVolume(72,false);Pump(50);
        Check(window.IsExpanded&&((Grid)window.FindName("Expanded")).Visibility==Visibility.Visible,"Delayed post-release Core Audio event cannot enter toast mode");
        using(var analysis=new AudioAnalysisService())
        {
            int frames=0;analysis.Changed+=_=>frames++;
            analysis.Configure(true,1);Pump(500);
            Console.WriteLine("CAPTURE SMOKE: "+analysis.Status+" frames="+frames);
            analysis.Configure(false,1);
            Check(analysis.Status=="Audio analysis off","Disabling audio releases capture");
        }
        window.Close();
        Check(!effects.IsAnimating,"Closing releases animation clocks");
    }
}
