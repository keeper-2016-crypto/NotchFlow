using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchFlow.Configuration;
using NotchFlow.Services;
using NotchFlow.Views;

internal static class SettingsWindowTests
{
 static void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);Console.WriteLine("PASS: "+label);}
 public static void Run(string root)
 {
  var store=new SettingsStore(System.IO.Path.Combine(root,"settings-redesign"));
  var original=new AppSettings{AccentMode="Red",WeatherCity="Пятигорск",BackgroundEffect="Liquid Glass"};
  int writes=0,previews=0;
  var window=new SettingsWindow(original,value=>{store.Save(value);writes++;},_=>previews++);
  window.Show();window.UpdateLayout();
  var tabs=(TabControl)window.FindName("Categories");
  Check(tabs.Items.Cast<TabItem>().Select(t=>t.Header.ToString()).SequenceEqual(new[]{"Общие","Внешний вид","Медиаплеер","Погода","О приложении"}),"Settings categories are Russian and complete");
  var draft=(AppSettings)window.DataContext;
  var directory=System.IO.Path.Combine(Environment.CurrentDirectory,".build","settings-redesign");System.IO.Directory.CreateDirectory(directory);
  for(int index=0;index<tabs.Items.Count;index++)
  {
   tabs.SelectedIndex=index;window.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var output=System.IO.File.Create(System.IO.Path.Combine(directory,$"category-{index}.png"));png.Save(output);
  }
  tabs.SelectedIndex=2;window.UpdateLayout();
  var accent=(ComboBox)window.FindName("AccentModeBox");
  Check((string)((ComboBoxItem)accent.SelectedItem).Content=="Красный","Localized accent retains legacy Red value");
  accent.SelectedValue="Blue";window.UpdateLayout();
  tabs.SelectedIndex=0;tabs.SelectedIndex=2;window.UpdateLayout();
  Check(draft.AccentMode=="Blue"&&writes==0&&previews==0&&original.AccentMode=="Red","Editing and navigation preserve draft without saving or preview");
  ((Button)window.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(writes==1&&window.IsVisible&&store.Load().AccentMode=="Blue","Apply saves through existing store and keeps settings open");
  draft.AccentMode="Green";window.Close();
  Check(store.Load().AccentMode=="Blue"&&writes==1&&previews==0,"Closing discards only unapplied changes");
  var compact=new SettingsWindow(store.Load(),_=>{});compact.Width=620;compact.Height=480;compact.Show();
  var compactTabs=(TabControl)compact.FindName("Categories");
  for(int index=0;index<compactTabs.Items.Count;index++)
  {
   compactTabs.SelectedIndex=index;compact.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)(compact.ActualWidth*1.5),(int)(compact.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(compact);
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var output=System.IO.File.Create(System.IO.Path.Combine(directory,$"compact-150-category-{index}.png"));png.Save(output);
  }
  Check(compact.ActualWidth==620&&compact.ActualHeight==480,"Settings supports compact viewport with scrollable categories");
  compact.Close();
  var invalid=new SettingsWindow(store.Load(),_=>writes++);invalid.Show();
  ((AppSettings)invalid.DataContext).CustomAccentColor="invalid";
  ((Button)invalid.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(invalid.IsVisible&&writes==1&&((TextBlock)invalid.FindName("ErrorText")).Text.Contains("Укажите"),"Invalid color prevents save and reports Russian validation");
  invalid.Close();
 }
}
