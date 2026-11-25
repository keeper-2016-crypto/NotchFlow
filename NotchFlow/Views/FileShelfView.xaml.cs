using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchFlow.Services;
using NotchFlow.ViewModels;
namespace NotchFlow.Views;
public partial class FileShelfView : UserControl
{
 Point start;bool dragging;public bool Animate {get;set;}
 FileShelfViewModel? Model=>DataContext as FileShelfViewModel;
 IslandWindow? Island=>Window.GetWindow(this) as IslandWindow;
 public FileShelfView(){InitializeComponent();}
 void PointerDown(object sender,MouseButtonEventArgs e)=>start=e.GetPosition(Files);
 async void PointerMove(object sender,MouseEventArgs e)
 {
  if(dragging||e.LeftButton!=MouseButtonState.Pressed||Model==null)return;
  var p=e.GetPosition(Files);if(Math.Abs(p.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(p.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
  if(FindButton(e.OriginalSource as DependencyObject))return;
  var selected=Files.SelectedItems.Cast<ShelfFileInfo>().ToArray();if(selected.Length==0)return;
  dragging=true;Island?.SetSettingsInteraction(this,true);
  try{
   var paths=selected.Where(i=>i.Available).Select(i=>i.Path).ToArray();if(paths.Length!=selected.Length){Model.Status="Недоступные элементы нельзя перетащить. Нажмите ↻.";return;}
   var data=new DataObject(DataFormats.FileDrop,paths);
   data.SetData("Preferred DropEffect",new MemoryStream(BitConverter.GetBytes((int)DragDropEffects.Copy)));
   // The receiving Windows application performs the operation; never delete originals here.
   DragDrop.DoDragDrop(Files,data,DragDropEffects.Copy|DragDropEffects.Move);
   await Model.RefreshAsync();
  }catch(Exception){Model.Status="Не удалось передать файлы. Оригиналы не изменены полкой.";}
  finally{dragging=false;Island?.SetSettingsInteraction(this,false);}
 }
 static bool FindButton(DependencyObject? d){while(d!=null){if(d is Button)return true;d=d is Visual?VisualTreeHelper.GetParent(d):null;}return false;}
 void MenuOpened(object sender,RoutedEventArgs e)=>Island?.SetSettingsInteraction(sender,true);
 void MenuClosed(object sender,RoutedEventArgs e)=>Island?.SetSettingsInteraction(sender,false);
 static ShelfFileInfo? Item(object sender)=>(sender as FrameworkElement)?.DataContext as ShelfFileInfo;
 async void RemoveClick(object sender,RoutedEventArgs e){if(Model==null||Item(sender) is not {} i)return;Island?.SetSettingsInteraction(sender,true);try{if(Animate&&sender is FrameworkElement f&&f.Parent is FrameworkElement row){row.BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(120)));await Task.Delay(120);row.BeginAnimation(OpacityProperty,null);}await Model.RemoveAsync(i.Path);}finally{Island?.SetSettingsInteraction(sender,false);}}
 async void RemoveMenuClick(object sender,RoutedEventArgs e){if(Model!=null&&Item(sender) is {} i)await Model.RemoveAsync(i.Path);}
 void ClearClick(object sender,RoutedEventArgs e){if(Model is {} m&&m.Items.Count>0)Island?.ConfirmDeletion("Очистить полку?",async()=>await m.ClearAsync(),"Исходные файлы и папки останутся на месте.","Очистить");}
 async void RefreshClick(object sender,RoutedEventArgs e){if(Model!=null)await Model.RefreshAsync();}
 async void OpenClick(object sender,RoutedEventArgs e)=>await OpenAsync(Item(sender),false);
 async void LocateClick(object sender,RoutedEventArgs e)=>await OpenAsync(Item(sender),true);
 async Task OpenAsync(ShelfFileInfo? item,bool locate)
 {
  if(item==null||Model==null)return;
  try{await Task.Run(()=>{var start=locate?new ProcessStartInfo("explorer.exe","/select,\""+item.Path+"\""):new ProcessStartInfo(item.Path);start.UseShellExecute=true;using var process=Process.Start(start);});}
  catch(Exception){Model.Status="Не удалось открыть файл: проверьте путь и доступ.";}
 }
 void RowLoaded(object sender,RoutedEventArgs e){if(Animate&&sender is FrameworkElement row)row.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(140)));}
}
