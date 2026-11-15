using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NotchFlow.Controls;
using NotchFlow.Configuration;
using NotchFlow.Services;
using NotchFlow.Views;
internal static class ColorPickerTests
{
 static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
 public static void Run(string root)
 {
  foreach(var color in new[]{Colors.Red,Colors.Lime,Colors.Blue,Colors.Cyan,Colors.Magenta,Colors.Yellow,Colors.White,Colors.Black,Color.FromRgb(37,83,129)})
  {var hsv=ColorMath.ToHsv(color);Check(ColorMath.FromHsv(hsv.H,hsv.S,hsv.V)==color,"HSV round trip "+ColorMath.Hex(color));}
  Check(ColorMath.TryHex("#aB12Ef",out var parsed)&&ColorMath.Hex(parsed)=="#AB12EF"&&!ColorMath.TryHex("#invalid",out _)&&!ColorMath.TryHex("#12345",out _),"HEX validation and normalization");
  var directory=System.IO.Path.Combine(root,"removed-modes");System.IO.Directory.CreateDirectory(directory);var store=new SettingsStore(directory);
  foreach(var old in new[]{"Album Glow","Breathing Island","Neon Ribbon"})
  {
   System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"settings.json"),System.Text.Json.JsonSerializer.Serialize(new {MediaVisualizer=old,CustomAccentColor="#123456",WeatherCity="Пятигорск",RibbonGlow="High",Scale=1.2}));
   var loaded=store.Load();Check(loaded.MediaVisualizer=="Classic"&&loaded.CustomAccentColor=="#123456"&&loaded.Scale==1.2,"Removed mode migrates without resetting other settings: "+old);
  }
  var picker=new ColorPickerWindow("#FF0000");picker.Show();picker.UpdateLayout();
  var hue=(Slider)picker.FindName("Hue");hue.Value=120;
  Check(picker.SelectedHex=="#00FF00"&&((TextBox)picker.FindName("HexInput")).Text=="#00FF00","Hue selection synchronizes HEX");
  var hex=(TextBox)picker.FindName("HexInput");hex.Text="#224466";
  Check(picker.SelectedHex=="#224466","HEX selection synchronizes selected color");
  hex.Text="bad";Check(!((Button)picker.FindName("Confirm")).IsEnabled,"Invalid HEX disables confirmation");hex.Text="#224466";
  picker.UpdateLayout();picker.Dispatcher.Invoke(()=>{},DispatcherPriority.Render);
  var image=new RenderTargetBitmap((int)picker.ActualWidth,(int)picker.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(picker);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=System.IO.File.Create(System.IO.Path.Combine(Environment.CurrentDirectory,".build","color-picker.png")))encoder.Save(file);
  picker.Close();
  var confirm=new ColorPickerWindow("#000000");
  confirm.Loaded+=(_,_)=>confirm.Dispatcher.BeginInvoke(new Action(()=>{((TextBox)confirm.FindName("HexInput")).Text="#ABCDEF";((Button)confirm.FindName("Confirm")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
  Check(confirm.ShowDialog()==true&&confirm.SelectedHex=="#ABCDEF","Picker Apply confirms chosen color");
  var cancel=new ColorPickerWindow("#123456");cancel.Loaded+=(_,_)=>cancel.Dispatcher.BeginInvoke(new Action(()=>{((Slider)cancel.FindName("Hue")).Value=240;cancel.Close();}));
  Check(cancel.ShowDialog()!=true,"Picker close does not confirm tentative color");
  store.Save(new AppSettings{CustomAccentColor=confirm.SelectedHex,GlassCustomTint="#224466",CapsuleColor="#112233",MediaVisualizer="Equalizer"});
  var saved=store.Load();Check(saved.CustomAccentColor=="#ABCDEF"&&saved.GlassCustomTint=="#224466"&&saved.CapsuleColor=="#112233"&&saved.MediaVisualizer=="Equalizer","All custom colors persist through settings reload");
 }
}
