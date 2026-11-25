using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NotchFlow.Helpers;
using NotchFlow.Services;
namespace NotchFlow.ViewModels;
public sealed class FileShelfViewModel : ObservableObject,IDisposable
{
 readonly FileShelfService service; readonly SemaphoreSlim mutations=new(1,1);readonly CancellationTokenSource lifetime=new();
 bool loaded,disposed;string status="Перетащите сюда файлы или папки";
 public ObservableCollection<ShelfFileInfo> Items {get;}=new();
 public string Status {get=>status;set=>Set(ref status,value);}
 public string CountText=>$"Файлы · {Items.Count}";
 public FileShelfViewModel(FileShelfService service){this.service=service;Items.CollectionChanged+=(_,_)=>Raise(nameof(CountText));}
 public async Task LoadAsync()
 {
  await mutations.WaitAsync();try{if(loaded||disposed)return;var paths=await service.LoadAsync();if(disposed)return;foreach(var path in paths)Items.Add(FileShelfService.Unavailable(path));loaded=true;Status=Items.Count==0?"Перетащите сюда файлы или папки":string.Empty;}
  catch(Exception){Status="Не удалось прочитать полку. Исходный файл сохранён; изменения заблокированы.";}
  finally{mutations.Release();}
  if(loaded)await RefreshAsync();
 }
 public async Task AddAsync(IEnumerable<string> paths)
 {
  var normalized=paths.Select(FileShelfService.Normalize).ToArray();
  await ChangeAsync(current=>current.Concat(normalized.Where(p=>p!=null).Cast<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
  if(normalized.Any(p=>p==null))Status="Некоторые пути не поддерживаются";
  await RefreshAsync();
 }
 public Task RemoveAsync(string path)=>ChangeAsync(current=>current.Where(p=>!string.Equals(p,path,StringComparison.OrdinalIgnoreCase)).ToArray());
 public Task ClearAsync()=>ChangeAsync(_=>Array.Empty<string>());
 async Task ChangeAsync(Func<string[],string[]> change)
 {
  await mutations.WaitAsync();try{
   if(disposed)return;if(!loaded){Status="Полка ещё не загружена; попробуйте снова";return;}
   var next=change(Items.Select(i=>i.Path).ToArray());await service.SaveAsync(next);if(disposed)return;
   var old=Items.ToDictionary(i=>i.Path,StringComparer.OrdinalIgnoreCase);Items.Clear();foreach(var p in next)Items.Add(old.TryGetValue(p,out var i)?i:FileShelfService.Unavailable(p));Status=Items.Count==0?"Перетащите сюда файлы или папки":string.Empty;
  }catch(Exception){Status="Не удалось сохранить полку. Предыдущее содержимое сохранено.";}finally{mutations.Release();}
 }
 bool refreshing;
 public async Task RefreshAsync()
 {
  if(refreshing||disposed)return;refreshing=true;
  try{foreach(var item in Items.ToArray()){var info=await service.InspectAsync(item.Path,lifetime.Token);if(disposed)return;int index=Items.IndexOf(item);if(index>=0)Items[index]=info;}}
  catch(OperationCanceledException){}finally{refreshing=false;}
 }
 public void Dispose(){disposed=true;lifetime.Cancel();lifetime.Dispose();}
}
