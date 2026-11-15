using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NotchFlow.Configuration;

namespace NotchFlow.Services;
public sealed class UpdateCoordinator : INotifyPropertyChanged,IDisposable
{
 readonly UpdateService service;
 readonly Func<AppSettings> settings;
 readonly string stateFile;
 readonly CancellationTokenSource lifetime=new();
 readonly SemaphoreSlim checkGate=new(1,1);
 UpdateState state=new();
 string status="Обновления ещё не проверялись.";
 string? notified;
 public event PropertyChangedEventHandler? PropertyChanged;
 public event Action<VerifiedUpdate>? Available;
 public UpdateService Service=>service;
 public bool IsAvailable=>true;
 public string CurrentVersion=>service.CurrentVersion.ToString(3);
 public string Status=>status;
 public string LastSuccess=>state.LastSuccess is {} date?date.ToLocalTime().ToString("dd.MM.yyyy HH:mm"):"Ещё не было";
 public UpdateCoordinator(UpdateService service,Func<AppSettings> settings,string? stateFile=null)
 {
  this.service=service;this.settings=settings;this.stateFile=stateFile??Path.Combine(Log.DirectoryPath,"Updates","state.json");
  try{if(File.Exists(this.stateFile))state=JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(this.stateFile))??new();}catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException){Log.Error(ex);}
 }
 public static bool IsDue(DateTimeOffset? attempt,DateTimeOffset now)=>attempt==null||attempt>now||now-attempt>=TimeSpan.FromHours(12);
 public void Start()=>_ = ScheduleAsync();
 async Task ScheduleAsync()
 {
  try
  {
   await Task.Delay(TimeSpan.FromSeconds(8),lifetime.Token);
   if(settings().AutoCheckUpdates&&settings().CheckUpdatesAtStartup)await CheckAsync(false);
   while(!lifetime.IsCancellationRequested)
   {
    await Task.Delay(TimeSpan.FromHours(12),lifetime.Token);
    if(settings().AutoCheckUpdates)await CheckAsync(false);
   }
  }
  catch(OperationCanceledException){}
 }
 void Persist()
 {
  try{Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);var tmp=stateFile+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(state));File.Move(tmp,stateFile,true);}
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Log.Error(ex);}
 }
 public void SetStatus(string value){status=value;PropertyChanged?.Invoke(this,new(nameof(Status)));PropertyChanged?.Invoke(this,new(nameof(LastSuccess)));}
 public async Task CheckAsync(bool manual)
 {
  if(!await checkGate.WaitAsync(0))return;
  try
  {
   if(!manual&&!IsDue(state.LastAttempt,DateTimeOffset.UtcNow))return;
   state.LastAttempt=DateTimeOffset.UtcNow;Persist();SetStatus("Проверяем наличие обновлений…");
   var release=await service.CheckAsync(lifetime.Token);
   state.LastSuccess=DateTimeOffset.UtcNow;Persist();
   SetStatus(release==null?"Установлена актуальная версия.":"Доступна версия "+release.Manifest.Version+".");
   if(release!=null&&(manual||notified!=release.Manifest.Version)){notified=release.Manifest.Version;Available?.Invoke(release);}
  }
  catch(OperationCanceledException){if(!lifetime.IsCancellationRequested)SetStatus("Проверка заняла слишком много времени. Повторите позже.");}
  catch(UpdateSecurityException ex){SetStatus(ex.Message);}
  catch(Exception ex){Log.Error(ex);SetStatus("Не удалось проверить обновления. Проверьте интернет и повторите позже.");}
  finally{checkGate.Release();}
 }
 public void Dispose(){lifetime.Cancel();service.Dispose();}
 public sealed class UpdateState{public DateTimeOffset? LastAttempt{get;set;}public DateTimeOffset? LastSuccess{get;set;}}
}
