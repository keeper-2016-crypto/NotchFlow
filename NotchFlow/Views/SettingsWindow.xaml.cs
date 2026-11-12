using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NotchFlow.Configuration;
using NotchFlow.Services;
namespace NotchFlow.Views;
public partial class SettingsWindow : Window
{
 readonly AppSettings draft;readonly Action<AppSettings> save;
 public SettingsWindow(AppSettings current,Action<AppSettings> save) {InitializeComponent();draft=current.Clone();this.save=save;var screens=new List<MonitorOption>{new("","Основной монитор (автоматически)")};screens.AddRange(System.Windows.Forms.Screen.AllScreens.Select(s=>new MonitorOption(s.DeviceName,$"{s.DeviceName} — {s.Bounds.Width} × {s.Bounds.Height}")));if(!screens.Any(s=>s.Id==draft.Monitor))screens.Add(new(draft.Monitor,"Отключённый монитор (временно основной)"));MonitorBox.ItemsSource=screens;DataContext=draft;}
 public sealed record MonitorOption(string Id,string Label);
 static bool HasErrors(DependencyObject parent) {if(Validation.GetHasError(parent))return true;foreach(var child in LogicalTreeHelper.GetChildren(parent)){if(child is DependencyObject dependency && HasErrors(dependency))return true;}for(int i=0;i<(parent is Visual ? VisualTreeHelper.GetChildrenCount(parent) : 0);i++)if(HasErrors(VisualTreeHelper.GetChild(parent,i)))return true;return false;}
 void ResetAppearanceClick(object sender,RoutedEventArgs e) {var defaults=new AppSettings();draft.AttachToTop=defaults.AttachToTop;draft.CapsuleColor=defaults.CapsuleColor;draft.Opacity=defaults.Opacity;draft.Scale=defaults.Scale;draft.CornerRadius=defaults.CornerRadius;draft.Shadow=defaults.Shadow;draft.Animations=defaults.Animations;draft.AnimationMs=defaults.AnimationMs;DataContext=null;DataContext=draft;}
 void SaveClick(object sender,RoutedEventArgs e) {try {System.Windows.Input.Keyboard.ClearFocus();if(HasErrors(this))throw new ArgumentException("Исправьте числовые поля, выделенные красным.");draft.Validate();save(draft);Close();}catch(Exception ex){Log.Error(ex);ErrorText.Text="Не удалось сохранить: "+ex.GetBaseException().Message+"\nПолная ошибка записана в журнал NotchFlow.";}}
}




