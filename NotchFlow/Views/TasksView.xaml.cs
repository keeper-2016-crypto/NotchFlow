using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NotchFlow.Models;
using NotchFlow.ViewModels;
namespace NotchFlow.Views;
public partial class TasksView : UserControl
{
 public TasksView()=>InitializeComponent();
 void AddTask(object sender,RoutedEventArgs e)=>(DataContext as PersonalViewModel)?.AddTask();
 void AddKey(object sender,KeyEventArgs e){if(e.Key==Key.Enter){(DataContext as PersonalViewModel)?.AddTask();e.Handled=true;}}
 void RemoveTask(object sender,RoutedEventArgs e){if(DataContext is PersonalViewModel vm&&((FrameworkElement)sender).DataContext is TaskItem item){if(Window.GetWindow(this) is IslandWindow window)window.ConfirmDeletion("Delete task?",()=>vm.RemoveTask(item));}}
 void EditTask(object sender,RoutedEventArgs e){if(((FrameworkElement)sender).DataContext is TaskItem item)item.IsEditing=!item.IsEditing;}
 void EditKey(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&((FrameworkElement)sender).DataContext is TaskItem item){item.IsEditing=false;(DataContext as PersonalViewModel)?.Flush();e.Handled=true;}}
 void Save(object sender,RoutedEventArgs e)=>(DataContext as PersonalViewModel)?.Flush();
 void CompletionChanged(object sender,RoutedEventArgs e){if(DataContext is PersonalViewModel{AnimationsEnabled:true}&&sender is CheckBox box&&box.IsLoaded)box.BeginAnimation(OpacityProperty,new DoubleAnimation(.3,1,TimeSpan.FromMilliseconds(180)));}
}
