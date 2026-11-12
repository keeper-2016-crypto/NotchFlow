using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using NotchFlow.Configuration;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Views;
using Forms=System.Windows.Forms;
namespace NotchFlow;
public partial class App : Application
{
 EventWaitHandle? activation;
 RegisteredWaitHandle? activationWait;
 Mutex? mutex;bool ownsMutex;readonly SettingsStore store=new();AppSettings settings=new();readonly MediaService media=new();readonly VolumeService volume=new();IslandWindow? island;SettingsWindow? settingsWindow;Forms.NotifyIcon? tray;System.Drawing.Icon? trayIcon;
 protected override async void OnStartup(StartupEventArgs e) {base.OnStartup(e);if(e.Args.Length==3&&e.Args[0]=="--settings-check"){if(e.Args[1]=="wait"){System.IO.Directory.CreateDirectory(e.Args[2]);System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[2],"ready"),"ready");await System.Threading.Tasks.Task.Delay(3000);}Shutdown(SettingsDiagnostics.Run(e.Args[1]=="wait"?"one":e.Args[1],e.Args[2]));return;}bool verify=e.Args.Length==2&&e.Args[0]=="--verify-startup";mutex=new Mutex(false,verify?@"Local\NotchFlow.Verify":@"Local\NotchFlow.Desktop");try{ownsMutex=mutex.WaitOne(0);}catch(AbandonedMutexException){ownsMutex=true;}if(!ownsMutex){try{using var existing=EventWaitHandle.OpenExisting(@"Local\NotchFlow.Activate");existing.Set();}catch(Exception ex){Log.Error(ex);}Shutdown();return;}DispatcherUnhandledException+=(_,args)=>{Log.Error(args.Exception);MessageBox.Show("NotchFlow завершает работу после ошибки. Подробности в %LOCALAPPDATA%\\NotchFlow\\errors.log","NotchFlow");args.Handled=true;Shutdown(1);};
  try {activation=new EventWaitHandle(false,EventResetMode.AutoReset,verify?@"Local\NotchFlow.VerifyActivate":@"Local\NotchFlow.Activate");activationWait=ThreadPool.RegisterWaitForSingleObject(activation,(_,_)=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(OpenSettings));},null,Timeout.Infinite,false);settings=store.Load();island=new IslandWindow(new IslandViewModel(media),settings,volume);MainWindow=island;SessionEnding+=(_,args)=>{if(island.TrySaveUserData()==false)args.Cancel=true;};island.SettingsRequested+=OpenSettings;volume.Changed+=island.ShowVolume;media.Failed+=island.ShowMessage;
   var menu=new Forms.ContextMenuStrip();menu.Items.Add("Show",null,(_,_)=>island.Reveal());menu.Items.Add("Settings",null,(_,_)=>OpenSettings());menu.Items.Add("Restart",null,(_,_)=>Restart());menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("Exit",null,(_,_)=>RequestExit());
   using(var iconStream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/NotchFlow.ico")).Stream){trayIcon=new System.Drawing.Icon(iconStream);}tray=new Forms.NotifyIcon{Icon=trayIcon,Text="NotchFlow",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(_,_)=>island.Reveal();island.Show();if(Array.Exists(e.Args,x=>x=="--settings"))OpenSettings();volume.Start();await media.StartAsync();if(verify){OpenSettings();await StartupDiagnostics.VerifyAsync(island,settingsWindow!,e.Args[1]);Shutdown();}
  } catch(Exception ex) {Log.Error(ex);MessageBox.Show(ex.Message,"NotchFlow — ошибка запуска");Shutdown(1);}
 }
 void OpenSettings() {if(settingsWindow!=null){settingsWindow.Activate();return;}settingsWindow=new SettingsWindow(settings,value=>{if(value.AutoStart!=settings.AutoStart)SettingsStore.SetAutoStart(value.AutoStart);try{store.Save(value);}catch{if(value.AutoStart!=settings.AutoStart)SettingsStore.SetAutoStart(settings.AutoStart);throw;}settings=value;island?.Apply(value);});settingsWindow.Closed+=(_,_)=>settingsWindow=null;settingsWindow.Show();}
 void RequestExit() {if(island?.TrySaveUserData()==false){MessageBox.Show("Заметки или задачи не сохранены. Повторите сохранение перед выходом.","NotchFlow");return;}Shutdown();}
 void Restart() {if(island?.TrySaveUserData()==false){MessageBox.Show("Не удалось сохранить заметки. Перезапуск отменён.","NotchFlow");return;}try {var exe=Environment.ProcessPath??throw new InvalidOperationException("Не найден путь приложения");if(ownsMutex){mutex!.ReleaseMutex();ownsMutex=false;}Process.Start(new ProcessStartInfo(exe){UseShellExecute=true});Shutdown();}catch(Exception ex){Log.Error(ex);island?.ShowMessage("Перезапуск не удался");}}
 protected override void OnExit(ExitEventArgs e) {activationWait?.Unregister(null);activation?.Dispose();if(tray!=null){tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();}trayIcon?.Dispose();volume.Dispose();media.Dispose();island?.Close();settingsWindow?.Close();if(ownsMutex)mutex?.ReleaseMutex();mutex?.Dispose();base.OnExit(e);}
}






