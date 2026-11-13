using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using NotchFlow.ViewModels;
namespace NotchFlow.Views;
public partial class NotesView : UserControl
{
 PersonalViewModel? model;
 public NotesView(){InitializeComponent();DataContextChanged+=(_,_)=>Attach();Unloaded+=(_,_)=>Detach();Loaded+=(_,_)=>Attach();}
 void Detach(){if(model!=null)model.PropertyChanged-=Changed;model=null;}
 void Attach(){Detach();model=DataContext as PersonalViewModel;if(model!=null)model.PropertyChanged+=Changed;}
 void Changed(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(PersonalViewModel.SelectedNote)&&model?.AnimationsEnabled==true)NoteContent.BeginAnimation(OpacityProperty,new DoubleAnimation(.4,1,TimeSpan.FromMilliseconds(180)));}
 void AddNote(object sender,RoutedEventArgs e)=>(DataContext as PersonalViewModel)?.AddNote();
 void Back(object sender,RoutedEventArgs e){if(DataContext is PersonalViewModel vm){vm.Flush();vm.SelectedNote=null;}}
 void Save(object sender,RoutedEventArgs e)=>(DataContext as PersonalViewModel)?.Flush();
 void DeleteNote(object sender,RoutedEventArgs e){if(DataContext is PersonalViewModel vm && vm.SelectedNote is {} note && Window.GetWindow(this) is IslandWindow window)window.ConfirmDeletion("Delete note?",()=>{if(vm.SelectedNote==note)vm.DeleteSelectedNote();});}
}
