using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NotchFlow.Services;
using NotchFlow.Views;
namespace NotchFlow;
public partial class App
{
 UpdateCoordinator? updates;
 UpdateWindow? updateWindow;
 bool installing;
 void InitializeUpdates(bool start)
 {
  updates=new UpdateCoordinator(new UpdateService(),()=>settings);
  updates.Available+=ShowUpdate;
  if(start)updates.Start();
 }
 void ShowUpdate(VerifiedUpdate release)
 {
  if(updateWindow!=null){updateWindow.Activate();return;}
  updateWindow=new UpdateWindow(updates!,release,InstallUpdateAsync);
  var window=updateWindow;
  island?.SetSettingsInteraction(window,true);
  window.Closed+=(_,_)=>{island?.SetSettingsInteraction(window,false);updateWindow=null;};window.Show();
 }
 async Task InstallUpdateAsync(VerifiedUpdate release,string path)
 {
  if(installing)throw new InvalidOperationException("Установка уже запускается.");
  installing=true;
  try
  {
   var installedDirectory=AppContext.BaseDirectory;
   if(!File.Exists(Path.Combine(installedDirectory,"unins000.exe")))throw new InvalidOperationException("Это переносная или тестовая сборка. Для обновления поверх приложения сначала установите NotchFlow обычным установщиком.");
   using var verified=await updates!.Service.OpenVerifiedInstallerAsync(release,path,CancellationToken.None);
   if(settingsWindow!=null&&!settingsWindow.SaveBeforeUpdate())throw new InvalidOperationException("Исправьте настройки перед установкой обновления.");
   store.Save(settings);
   if(island?.TrySaveUserData()==false)throw new InvalidOperationException("Не удалось сохранить заметки и задачи. Обновление отменено.");
   var start=new ProcessStartInfo(path){UseShellExecute=true};
   start.ArgumentList.Add("/UPDATEAPP=1");start.ArgumentList.Add("/DIR="+installedDirectory.TrimEnd(Path.DirectorySeparatorChar));
   // Interactive Inno Setup runs at the existing user's privilege level. No silent or force-close flags.
   using var process=Process.Start(start)??throw new InvalidOperationException("Не удалось запустить установщик. Приложение продолжает работу.");
   Shutdown();
  }
  finally{installing=false;}
 }
 void DisposeUpdates(){updates?.Dispose();updateWindow?.Close();}
}
