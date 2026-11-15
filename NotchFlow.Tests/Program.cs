using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using NotchFlow;
using NotchFlow.Configuration;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Views;

internal static class Program
{
 [STAThread] static int Main() {
  if(Environment.GetEnvironmentVariable("NOTCHFLOW_VERIFY_UPDATE_SERVER")=="1"){
   using var check=new UpdateService();try{var result=check.CheckAsync(default).GetAwaiter().GetResult();Console.WriteLine(result==null?"No newer signed update":"New signed update; no download or installation performed");return 0;}catch(UpdateSecurityException ex){Console.WriteLine("REJECTED: "+ex.Message);return 0;}
  }
  if(Environment.GetEnvironmentVariable("NOTCHFLOW_COLOR_PICKER_PROBE")=="1"){
   var probe=new Application();probe.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/NotchFlow;component/Themes/Dark.xaml",UriKind.Relative)});probe.Run(new ColorPickerWindow("#FF3333"));return 0;
  }
  if(Environment.GetEnvironmentVariable("NOTCHFLOW_BACKDROP_PROBE")=="1"){BackdropProbe.Run();return 0;}
  var directory=Path.Combine(Environment.CurrentDirectory,"work","NotchFlow-test-"+Guid.NewGuid().ToString("N"));
  Environment.SetEnvironmentVariable("NOTCHFLOW_DATA_DIR",directory);
  try {
   var store=new SettingsStore(directory); Check(store.Load().Scale==1,"Default configuration");
   var settings=new AppSettings{Scale=9,Opacity=-2,AnimationMs=1,Position="Invalid"};settings.Validate();Check(settings.Scale==1.8&&settings.Opacity==.3&&settings.AnimationMs==150&&settings.Position=="Center","Settings bounds");
   bool rejected=false;try{new AppSettings{Scale=double.NaN}.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Reject NaN");
   store.Save(new AppSettings{OffsetX=-40,Monitor="TEST",Animations=false});var loaded=store.Load();Check(loaded.OffsetX==-40&&loaded.Monitor=="TEST"&&!loaded.Animations,"JSON round trip");
   File.WriteAllText(Path.Combine(directory,"settings.json"),"{invalid json");Check(store.Load().Scale==1,"Corrupt settings recovery");Check(Directory.GetFiles(directory,"*.corrupt-*").Length==1,"Corrupt file backup");
   Stage3Tests.Core(directory);AudioTests.Run();UpdateTests.Run(directory).GetAwaiter().GetResult();
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/NotchFlow;component/Themes/Dark.xaml",UriKind.Relative)});UpdateUiTests.Run(directory);ColorPickerTests.Run(directory);SettingsWindowTests.Run(directory);GlassTests.Run(directory);VisualTests.Run(directory);Stage3Tests.Desktop(directory);using var media=new MediaService();var vm=new IslandViewModel(media,dataDirectory:directory);Check(vm.Title=="Ничего не воспроизводится"&&!vm.CanToggle&&!vm.CanNext,"Empty media state");
   var window=new IslandWindow(vm,new AppSettings{Animations=false});window.Show();window.UpdateLayout();window.Reveal();window.UpdateLayout();Check(window.IsVisible,"WPF window and bindings load"); Capture(window,"expanded-100.png");
   window.ShowVolume(45,false);window.UpdateLayout();Check(((Grid)window.FindName("Expanded")).Visibility==Visibility.Visible,"Late volume notification preserves expanded player");
   window.Apply(new AppSettings{Animations=false,Scale=1.5,MediaEnabled=false,Monitor="DISCONNECTED",VolumeEnabled=false});window.UpdateLayout();Check(Math.Abs(window.Width-660)<.01&&vm.Title=="Медиаплеер отключён","Apply scale, disabled media and monitor fallback");Capture(window,"expanded-150.png");window.Close();
   var appliedWindow=new IslandWindow(new IslandViewModel(media),new AppSettings{Animations=false});appliedWindow.Show();var settingsWindow=new SettingsWindow(new(){Animations=false},value=>{store.Save(value);appliedWindow.Apply(value);});settingsWindow.Show();settingsWindow.UpdateLayout();Check(settingsWindow.IsVisible,"Settings XAML loads");Capture(settingsWindow,"settings.png");var draft=(AppSettings)settingsWindow.DataContext;draft.Scale=1.1;draft.OffsetX=31;draft.CapsuleColor="#232427";((Button)settingsWindow.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Console.WriteLine("Save status: "+((TextBlock)settingsWindow.FindName("ErrorText")).Text+" width="+appliedWindow.Width+" x="+store.Load().OffsetX+" color="+store.Load().CapsuleColor);Check(Math.Abs(appliedWindow.Width-484)<.01&&store.Load().OffsetX==31&&store.Load().CapsuleColor=="#232427","Settings Save button persists and applies without restart");appliedWindow.Close();
   var motionWindow=new IslandWindow(new IslandViewModel(media),new AppSettings{Animations=true});motionWindow.Show();Pump(300);Capture(motionWindow,"compact.png");motionWindow.Reveal();Pump(100);var capsule=(Border)motionWindow.FindName("Capsule");Check(capsule.ActualWidth>212&&capsule.ActualWidth<401,"Animated intermediate geometry");Capture(motionWindow,"opening.png");Pump(300);Check(Math.Abs(capsule.ActualWidth-400)<1,"Animation settles at target");Capture(motionWindow,"expanded-final.png");motionWindow.Close();motionWindow=new IslandWindow(new IslandViewModel(media),new AppSettings{Animations=false,AttachToTop=true});motionWindow.Show();motionWindow.Reveal();motionWindow.UpdateLayout();Capture(motionWindow,"notch.png");Check(((NotchFlow.Controls.IslandSurface)motionWindow.FindName("Capsule")).IsNotch,"Notch geometry loads");motionWindow.Close();Console.WriteLine("Baseline suite completed");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
  finally {try {Directory.Delete(directory,true);}catch{}}
 }
 static void Pump(int ms){var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);}
 static void Capture(Window window,string name) {var directory=Path.Combine(Environment.CurrentDirectory,".build","screenshots");Directory.CreateDirectory(directory);window.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,name));png.Save(file); }
 static void Check(bool condition,string name) {if(!condition)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
}





