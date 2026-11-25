using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NotchFlow.Configuration;
using NotchFlow.Services;
namespace NotchFlow.Views;
public partial class SettingsWindow : Window
{
 readonly AppSettings draft;
 readonly Action<AppSettings> save;
 public UpdateCoordinator? Updates{get;}
 public SettingsWindow(AppSettings current,Action<AppSettings> save,Action<AppSettings>? preview=null,UpdateCoordinator? updates=null)
 {
  Updates=updates;InitializeComponent();ApplyAccent(AlbumColor.Accent(current,AlbumColor.Palette(null)));draft=current.Clone();this.save=save;
  var screens=new List<MonitorOption>{new("","Основной монитор (автоматически)")};
  screens.AddRange(System.Windows.Forms.Screen.AllScreens.Select((s,i)=>new MonitorOption(s.DeviceName,$"Монитор {i+1} — {s.Bounds.Width} × {s.Bounds.Height}")));
  if(!screens.Any(s=>s.Id==draft.Monitor))screens.Add(new(draft.Monitor,"Отключённый монитор (временно основной)"));
  MonitorBox.ItemsSource=screens;DataContext=draft;
  VersionText.Text="Версия "+typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3);
  MaxHeight=SystemParameters.WorkArea.Height;MaxWidth=SystemParameters.WorkArea.Width;
  Height=Math.Min(Height,MaxHeight);Width=Math.Min(Width,MaxWidth);
 }

 public void ApplyAccent(Color color)
 {
  Resources["SettingsAccent"]=new SolidColorBrush(color);
  Resources["SettingsAccentTint"]=new SolidColorBrush(Color.FromRgb((byte)(20+color.R*.31),(byte)(20+color.G*.31),(byte)(20+color.B*.31)));
 }
 [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
 static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 protected override void OnSourceInitialized(EventArgs e)
 {
  base.OnSourceInitialized(e);
  var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
  int dark=1,caption=0x0C0B0B;
  DwmSetWindowAttribute(hwnd,20,ref dark,4);
  DwmSetWindowAttribute(hwnd,35,ref caption,4);
 }
 public sealed record MonitorOption(string Id,string Label);
 static bool HasErrors(DependencyObject parent) {if(Validation.GetHasError(parent))return true;foreach(var child in LogicalTreeHelper.GetChildren(parent)){if(child is DependencyObject dependency && HasErrors(dependency))return true;}for(int i=0;i<(parent is Visual ? VisualTreeHelper.GetChildrenCount(parent) : 0);i++)if(HasErrors(VisualTreeHelper.GetChild(parent,i)))return true;return false;}
 void ResetAppearanceClick(object sender,RoutedEventArgs e)
 {
  var defaults=new AppSettings();
  foreach(var name in new[]{"AttachToTop","CapsuleColor","Opacity","Scale","CornerRadius","Shadow","Animations","AnimationMs","BackgroundEffect","GlassBackdropBlur","GlassOpacity","GlassTint","GlassCustomTint","GlassReflection","GlassEdgeHighlight","GlassDynamicLighting","TintIntensity","BorderGlow","ReduceMotion","Monitor","Position","OffsetX","OffsetY"})
  {var property=typeof(AppSettings).GetProperty(name)!;property.SetValue(draft,property.GetValue(defaults));}
  DataContext=null;DataContext=draft;
  ErrorText.Text="Оформление сброшено в черновике. Нажмите «Применить» или «Сохранить».";
 }
 bool Commit()
 {
  try
  {
   System.Windows.Input.Keyboard.ClearFocus();
   if(HasErrors(this))throw new ArgumentException("Исправьте числовые поля, выделенные красным.");
   var value=draft.Clone();value.Validate();save(value);
   foreach(var property in typeof(AppSettings).GetProperties())if(property.CanWrite)property.SetValue(draft,property.GetValue(value));
   DataContext=null;DataContext=draft;
   ErrorText.Text="Настройки применены и сохранены.";return true;
  }
  catch(Exception ex){Log.Error(ex);ErrorText.Text=ex is ArgumentException?ex.Message:"Не удалось сохранить настройки. Подробности записаны в журнал NotchFlow.";return false;}
 }
 void SaveClick(object sender,RoutedEventArgs e){if(Commit())Close();}
 void ApplyClick(object sender,RoutedEventArgs e)=>Commit();
 public bool SaveBeforeUpdate()=>Commit();
 async void CheckUpdatesClick(object sender,RoutedEventArgs e){if(Updates!=null)await Updates.CheckAsync(true);}
 void CancelClick(object sender,RoutedEventArgs e)=>Close();
 void OpenLinkClick(object sender,RoutedEventArgs e)
 {
  try{if(sender is Button {Tag:string url} && url.StartsWith("https://github.com/keeper-2016-crypto/NotchFlow",StringComparison.Ordinal))Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
  catch(Exception ex){Log.Error(ex);ErrorText.Text="Не удалось открыть ссылку в браузере.";}
 }
}
