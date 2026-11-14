using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchFlow.Configuration;
using NotchFlow.Controls;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Views;
internal static class GlassTests
{
 static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
 public static void Run(string root)
 {
  var settings=new AppSettings{BackgroundEffect="Liquid Glass",GlassOpacity=.88,GlassTint="Neutral",GlassReflection="High",Animations=false,CollapseOnLeave=false};
  var store=new SettingsStore(System.IO.Path.Combine(root,"glass"));store.Save(settings);
  Check(store.Load().GlassTint=="Neutral"&&store.Load().GlassOpacity==.88,"Glass settings round trip");
  bool rejected=false;try{new AppSettings{GlassOpacity=double.NaN}.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Glass rejects invalid opacity");
  using var media=new MediaService();
  var window=new IslandWindow(new IslandViewModel(media,dataDirectory:System.IO.Path.Combine(root,"glass-data")),settings);
  window.Show();window.Reveal();window.UpdateLayout();
  var layer=(LiquidGlassSurface)window.FindName("GlassLayer");
  Check(!layer.IsHitTestVisible&&window.Opacity==1,"Glass leaves input and foreground opacity untouched");
  var directory=System.IO.Path.Combine(Environment.CurrentDirectory,".build","glass-screenshots");System.IO.Directory.CreateDirectory(directory);
  foreach(bool notch in new[]{false,true})
  foreach(var background in new[]{"black","white","color","text"})
  {
   settings.AttachToTop=notch;window.Apply(settings);window.UpdateLayout();
   var visual=new DrawingVisual();
   using(var dc=visual.RenderOpen())
   {
    Brush brush=background switch {"white"=>Brushes.White,"color"=>new LinearGradientBrush(Colors.DarkBlue,Colors.Goldenrod,40),"text"=>Brushes.WhiteSmoke,_=>Brushes.Black};
    dc.DrawRectangle(brush,null,new Rect(0,0,440,210));
    if(background=="text")for(int y=4;y<200;y+=14)dc.DrawText(new FormattedText("Behind the island — sample browser text",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.Black,1),new Point(0,y));
    dc.DrawRectangle(new VisualBrush((Visual)window.Content){Stretch=Stretch.None,AlignmentY=AlignmentY.Top},null,new Rect(0,0,440,432));
   }
   foreach(double scale in new[]{1d,1.25,1.5,2d})
   {
    var bitmap=new RenderTargetBitmap((int)(440*scale),(int)(210*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var output=System.IO.File.Create(System.IO.Path.Combine(directory,$"{notch}-{background}-{scale}.png"));encoder.Save(output);
   }
  }
  foreach(var mode in new[]{"Classic","Album Glow","Breathing Island","Equalizer","Neon Ribbon"})
  {settings.MediaVisualizer=mode;window.Apply(settings);window.UpdateLayout();Check(layer.Visibility==Visibility.Visible,"Glass compatible with "+mode);}
  window.Close();
  foreach(bool notch in new[]{false,true})StableBackdropDuringTransition(root,notch);
 }
 static void StableBackdropDuringTransition(string root,bool notch)
 {
  using var media=new MediaService();
  var settings=new AppSettings{BackgroundEffect="Liquid Glass",GlassBackdropBlur=true,Animations=true,AnimationMs=500,StartCollapsed=true,CollapseOnLeave=false,AttachToTop=notch};
  var window=new IslandWindow(new IslandViewModel(media,dataDirectory:System.IO.Path.Combine(root,"transition")),settings);
  try
  {
   window.Show();Pump(650);
   var backdrop=(NativeBackdrop)typeof(IslandWindow).GetField("nativeBackdrop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
   if(!backdrop.Available){Console.WriteLine("SKIP: native backdrop transition requires supported desktop composition");return;}
   var source=(System.Windows.Interop.HwndSource)typeof(NativeBackdrop).GetField("source",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(backdrop)!;
   if(!GetWindowRect(source.Handle,out var initial))throw new InvalidOperationException("Cannot inspect native backdrop bounds");
   var capsule=(IslandSurface)window.FindName("Capsule");
   double compactWidth=capsule.ActualWidth;
   window.Reveal();
   bool intermediate=false;
   for(int i=0;i<22;i++)
   {
    Pump(30);
    intermediate|=capsule.ActualWidth>compactWidth+1&&capsule.ActualWidth<399;
    if(!GetWindowRect(source.Handle,out var current)||!current.Equals(initial)||!IsWindowVisible(source.Handle))
     throw new InvalidOperationException("Native backdrop resized or disappeared during opening");
   }
   Check(intermediate||!SystemParameters.ClientAreaAnimation,"Backdrop opening exercises intermediate geometry "+notch);
   typeof(IslandWindow).GetMethod("SetExpanded",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,new object[]{false,"GlassTests"});
   for(int i=0;i<22;i++)
   {
    Pump(30);
    if(!GetWindowRect(source.Handle,out var current)||!current.Equals(initial)||!IsWindowVisible(source.Handle))
     throw new InvalidOperationException("Native backdrop resized or disappeared during closing");
   }
   Check(Math.Abs(capsule.ActualWidth-compactWidth)<1,"Stable native backdrop through opening and closing "+notch);
  }
  finally{window.Close();}
 }
 static void Pump(int milliseconds)
 {
  var frame=new System.Windows.Threading.DispatcherFrame();
  var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};
  timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
 }
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
 struct NativeRect{public int Left,Top,Right,Bottom;}
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
}
