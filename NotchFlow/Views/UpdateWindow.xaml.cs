using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NotchFlow.Services;
namespace NotchFlow.Views;
public partial class UpdateWindow : Window
{
 readonly UpdateCoordinator updates;
 readonly VerifiedUpdate release;
 readonly Func<VerifiedUpdate,string,Task> install;
 readonly CancellationTokenSource cancellation=new();
 string? installer;
 bool busy,closed;
 public UpdateWindow(UpdateCoordinator updates,VerifiedUpdate release,Func<VerifiedUpdate,string,Task> install)
 {
  InitializeComponent();this.updates=updates;this.release=release;this.install=install;
  VersionsText.Text=$"Установлена версия: {updates.CurrentVersion}\nДоступна версия: {release.Manifest.Version}";NotesText.Text=release.Manifest.ReleaseNotes;
  MaxHeight=SystemParameters.WorkArea.Height;
 }
 async void ActionClick(object sender,RoutedEventArgs e)
 {
  if(busy)return;busy=true;ActionButton.IsEnabled=false;
  try
  {
   if(installer==null)
   {
    LaterButton.Content="Отменить загрузку";Progress.Visibility=Visibility.Visible;StatusText.Text="Загружаем и проверяем установщик…";
    installer=await updates.Service.DownloadAsync(release,new Progress<UpdateProgress>(p=>{if(closed)return;Progress.Value=p.Percent;ProgressText.Text=$"{p.Percent:0}% · {p.Received/1048576d:0.0} из {p.Total/1048576d:0.0} МБ";}),cancellation.Token);
    if(!closed){StatusText.Text="Обновление загружено. Установить сейчас?";ActionButton.Content="Установить";LaterButton.Content="Позже";updates.SetStatus("Обновление загружено и проверено.");}
   }
   else{StatusText.Text="Сохраняем данные и запускаем установщик…";await install(release,installer);}
  }
  catch(OperationCanceledException){if(!closed)StatusText.Text="Загрузка отменена.";}
  catch(Exception ex){Log.Error(ex);if(!closed)StatusText.Text=ex is UpdateSecurityException or InvalidOperationException?ex.Message:"Не удалось загрузить обновление. Проверьте соединение и повторите попытку.";}
  finally{busy=false;if(!closed){ActionButton.IsEnabled=!cancellation.IsCancellationRequested;LaterButton.Content="Позже";}}
 }
 void LaterClick(object sender,RoutedEventArgs e)=>Close();
 protected override void OnClosed(EventArgs e){closed=true;cancellation.Cancel();base.OnClosed(e);}
}
