using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NotchFlow.Models;
using NotchFlow.Services;
using NotchFlow.ViewModels;
namespace NotchFlow.Views;
public partial class IslandWindow
{
 FileShelfViewModel shelf=null!;
 readonly SystemMonitorViewModel monitorModel=new();
 readonly SystemMonitorService monitorService=new();
 readonly KeyboardToggleService keyboard=new();
 readonly DispatcherTimer keyboardTimer=new();
 bool dragHintActive; DispatcherOperation? pendingDragLeave;
 readonly object incomingDrag=new();
 bool keyboardNotice,shelfStarted;
 bool ShowMusicMetrics => settings.SystemMonitorEnabled && !expanded && !vm.UseClock && !showingToast && !keyboardNotice && (settings.MonitorCpu || settings.MonitorGpu || settings.MonitorRam);
 void UpdateMusicMetrics(){
  MiniCpu.Visibility=settings.MonitorCpu ? Visibility.Visible : Visibility.Collapsed;
  MiniCpuValue.Text=monitorModel.CpuText.Replace("Недоступно","—");
   MiniGpu.Visibility=settings.MonitorGpu ? Visibility.Visible : Visibility.Collapsed;
  MiniGpuValue.Text=monitorModel.GpuText.Replace("Недоступно","—");
   MiniRam.Visibility=settings.MonitorRam ? Visibility.Visible : Visibility.Collapsed;
  MiniRamValue.Text=monitorModel.RamText.Replace("Недоступно","—");
  MusicMetrics.Visibility=ShowMusicMetrics?Visibility.Visible:Visibility.Collapsed;
 }
 bool ShowSystemCompact => settings.SystemMonitorEnabled && vm.UseClock && monitorModel.Compact(settings.CompactSystemMetric,settings.MonitorCpu,settings.MonitorGpu,settings.MonitorRam).Length>0;
 void InitializeFeatures()
 {
  shelf=new(new FileShelfService(vm.DataDirectory));FilesPane.DataContext=shelf;SystemPane.DataContext=monitorModel;
  keyboard.Changed+=ShowKeyboardToggle;keyboardTimer.Tick+=KeyboardExpired;monitorService.Changed+=MetricsArrived;
  Deactivated+=DragInterrupted; Capsule.PreviewDragEnter+=FileDragOver; Capsule.AllowDrop=true;Capsule.PreviewDragOver+=FileDragOver;Capsule.PreviewDragLeave+=FileDragLeave;Capsule.PreviewDrop+=FileDrop;
  Loaded+=FeaturesLoaded;IsVisibleChanged+=FeaturesVisibility;
 }
 void FeaturesLoaded(object sender,RoutedEventArgs e)=>ConfigureFeatures();
 void FeaturesVisibility(object sender,DependencyPropertyChangedEventArgs e)=>ConfigureMonitor();
 void ConfigureFeatures()
 {
  if(closing)return;
  keyboard.Configure(settings.CapsLockNotifications,settings.NumLockNotifications);
  if(keyboardNotice&&(!settings.CapsLockNotifications&&!settings.NumLockNotifications || KeyboardText.Text.StartsWith("Caps Lock")&&!settings.CapsLockNotifications || KeyboardText.Text.StartsWith("Num Lock")&&!settings.NumLockNotifications)){keyboardTimer.Stop();keyboardNotice=false;}
  FilesTab.Visibility=settings.FileShelfEnabled?Visibility.Visible:Visibility.Collapsed;
  SystemTab.Visibility=settings.SystemMonitorEnabled?Visibility.Visible:Visibility.Collapsed;
  FilesPane.Animate=Duration>0;SystemPane.Configure(settings);
  if(!settings.FileShelfEnabled){EndIncomingDrag();if(vm.SelectedTab=="Files")vm.SelectedTab="Music";}
  if(!settings.SystemMonitorEnabled&&vm.SelectedTab=="System")vm.SelectedTab="Music";
  if(settings.FileShelfEnabled&&IsLoaded&&!shelfStarted){shelfStarted=true;_=shelf.LoadAsync();}
  ConfigureMonitor();
 }
 void ConfigureMonitor()
 {
  bool needed=!closing&&IsVisible&&settings.SystemMonitorEnabled&&(expanded&&vm.SelectedTab=="System"||ShowMusicMetrics||!expanded&&ShowSystemCompact);
  monitorService.Configure(needed,settings.MonitorGpu,settings.MonitorIntervalSeconds);
 }
 void MetricsArrived(SystemMetrics value)
 {
  if(Dispatcher.HasShutdownStarted)return;
  Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{if(closing)return;monitorModel.Accept(value);UpdateMusicMetrics();SystemCompactText.Text=monitorModel.Compact(settings.CompactSystemMetric,settings.MonitorCpu,settings.MonitorGpu,settings.MonitorRam);}));
 }
 void RenderFeatures(int version)
 {
  UpdateMusicMetrics();
  Fade(FilesPane,vm.SelectedTab=="Files"&&settings.FileShelfEnabled,version);
  Fade(SystemPane,vm.SelectedTab=="System"&&settings.SystemMonitorEnabled,version);
  Fade(SystemCompact,!expanded&&!showingToast&&!keyboardNotice&&ShowSystemCompact,version);
  SystemCompactText.Text=monitorModel.Compact(settings.CompactSystemMetric,settings.MonitorCpu,settings.MonitorGpu,settings.MonitorRam);
  EffectsLayer.Visibility = keyboardNotice ? Visibility.Collapsed : Visibility.Visible;
  Fade(KeyboardBanner,keyboardNotice,version);KeyboardBanner.IsHitTestVisible=false;
  ConfigureMonitor();
 }
 void SystemCompactClick(object sender,MouseButtonEventArgs e){vm.SelectedTab="System";SetExpanded(true);e.Handled=true;}
 void ShowKeyboardToggle(string key,bool enabled)
 {
  if(closing || HasInteractiveOverlay)return;
  KeyboardText.Text=key+(enabled?" включён":" выключен");keyboardNotice=true;
  showingToast=false;toastTimer.Stop();hover.Stop();keyboardTimer.Stop();keyboardTimer.Interval=TimeSpan.FromMilliseconds(settings.KeyboardNotificationMs);
  RenderState();keyboardTimer.Start();
 }
 void KeyboardExpired(object? sender,EventArgs e){keyboardTimer.Stop();keyboardNotice=false;if(!closing)RenderState();}
 void FileDragOver(object sender,DragEventArgs e)
 {
  e.Handled=true;e.Effects=DragDropEffects.None;if(!settings.FileShelfEnabled||closing)return;
  bool supported=false;try{supported=e.Data.GetDataPresent(DataFormats.FileDrop,false)&&(e.AllowedEffects&DragDropEffects.Copy)!=0;}catch{ }
  pendingDragLeave?.Abort(); pendingDragLeave=null;
  dragHintActive=true; DropZone.Margin=new Thickness(0,ContentHeight+1,0,0);
  interactions.Set(incomingDrag,true); // Reuses the same lock as volume, popups and confirmations.
  if(DropZone.Visibility!=Visibility.Visible){DropZone.Visibility=Visibility.Visible;NotchFlow.Animations.Motion.To(DropZone,OpacityProperty,1,Duration);}
  DropHint.Text=supported?"Отпустите файлы на полку":"Нужен файл или папка из Проводника";
  if(supported)e.Effects=DragDropEffects.Copy; // Adding a reference must never tell the source to delete a file.
 }
 void FileDragLeave(object sender,DragEventArgs e)
 {
  // A child-to-child transition produces leave followed by enter. Only a terminal leave ends the drag.
  pendingDragLeave?.Abort();
  pendingDragLeave=Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(EndIncomingDrag));
 }
 void DragInterrupted(object? sender,EventArgs e)=>EndIncomingDrag();
 void EndIncomingDrag()
 {
  pendingDragLeave?.Abort(); pendingDragLeave=null;
  if(!dragHintActive)return;
  dragHintActive=false;
  if(!closing)Resize();
  DropZone.BeginAnimation(OpacityProperty,null);DropZone.Opacity=0;DropZone.Visibility=Visibility.Collapsed;interactions.Set(incomingDrag,false);
 }
 async void FileDrop(object sender,DragEventArgs e)
 {
  e.Handled=true;e.Effects=DragDropEffects.None;
  try{
   if(!settings.FileShelfEnabled)return;
   if(e.Data.GetDataPresent(DataFormats.FileDrop,false)&&e.Data.GetData(DataFormats.FileDrop,false) is string[] {Length:>0} paths&&(e.AllowedEffects&DragDropEffects.Copy)!=0)
   {e.Effects=DragDropEffects.Copy;EndIncomingDrag();vm.SelectedTab="Files";SetExpanded(true);await shelf.LoadAsync();await shelf.AddAsync(paths);}
   else shelf.Status="Виртуальные файлы не поддерживаются. Сначала сохраните файл в Проводнике.";
  }catch(Exception){shelf.Status="Не удалось прочитать перетаскиваемые файлы.";}
  finally{EndIncomingDrag();}
 }
 void DisposeFeatures()
 {
  Deactivated-=DragInterrupted; Capsule.PreviewDragEnter-=FileDragOver; EndIncomingDrag();
  Loaded-=FeaturesLoaded;IsVisibleChanged-=FeaturesVisibility;
  keyboard.Changed-=ShowKeyboardToggle;keyboard.Dispose();keyboardTimer.Stop();keyboardTimer.Tick-=KeyboardExpired;
  monitorService.Changed-=MetricsArrived;monitorService.Dispose();shelf.Dispose();
  Capsule.PreviewDragOver-=FileDragOver;Capsule.PreviewDragLeave-=FileDragLeave;Capsule.PreviewDrop-=FileDrop;
 }
}
