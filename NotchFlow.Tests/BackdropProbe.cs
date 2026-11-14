using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NotchFlow.Configuration;
using NotchFlow.Models;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Views;

internal static class BackdropProbe
{
 public static void Run()
 {
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/NotchFlow;component/Themes/Dark.xaml",UriKind.Relative)});
  var grid=new Grid();
  for(int i=0;i<40;i++)grid.Children.Add(new Border{Background=i%2==0?Brushes.White:Brushes.Black,Width=12,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(i*12,0,0,0)});
  var scene=new Window{Title="NotchFlow blur test background",Width=720,Height=440,Left=100,Top=200,Content=grid,Background=Brushes.DarkOrange};
  scene.Show();
  using var media=new MediaService();
  var model=new IslandViewModel(media,dataDirectory:System.IO.Path.Combine(Environment.CurrentDirectory,".build","native-probe-data"));
  model.AcceptSnapshot(new MediaSnapshot("Native backdrop","Sharp WPF foreground",null,true,true,true,true,true,TimeSpan.FromSeconds(40),TimeSpan.FromSeconds(180),DateTimeOffset.UtcNow,1,"probe","track"));
  var settings=new AppSettings{BackgroundEffect="Liquid Glass",GlassOpacity=.35,AttachToTop=Environment.GetEnvironmentVariable("NOTCHFLOW_PROBE_NOTCH")=="1",CollapseOnLeave=false,StartCollapsed=false};
  var foreground=new IslandWindow(model,settings);
  foreground.Show();foreground.Reveal();foreground.Left=180;foreground.Top=270;
  var button=new Button{Content="Toggle Notch",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
  button.Click+=(_,_)=>{settings.AttachToTop=!settings.AttachToTop;foreground.Apply(settings);foreground.Left=180;foreground.Top=270;};
  grid.Children.Add(button);
  var cycle=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
  if(Environment.GetEnvironmentVariable("NOTCHFLOW_PROBE_ANIMATE")=="1")
  {
   settings.AnimationMs=1000;
   var expand=typeof(IslandWindow).GetMethod("SetExpanded",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
   cycle.Tick+=(_,_)=>expand.Invoke(foreground,new object[]{!foreground.IsExpanded,"BackdropProbe"});
   cycle.Start();
  }
  scene.Closed+=(_,_)=>{cycle.Stop();foreground.Close();app.Shutdown();};
  app.Run();
 }
}
