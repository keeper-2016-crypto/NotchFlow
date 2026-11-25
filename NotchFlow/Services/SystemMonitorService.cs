using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NotchFlow.Models;
namespace NotchFlow.Services;
public interface ISystemMetricSource : IDisposable {SystemMetrics Read();}
public sealed class WindowsMetricSource : ISystemMetricSource
{
 readonly PdhCounterSet counters;readonly GpuAdapters.Adapter[] adapters;readonly string cpuName;
 public WindowsMetricSource(bool gpu)
 {
  counters=new(gpu);adapters=gpu?GpuAdapters.Enumerate():Array.Empty<GpuAdapters.Adapter>();
  try{using var key=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");cpuName=(key?.GetValue("ProcessorNameString") as string)?.Trim()??"Процессор";}catch{cpuName="Процессор";}
 }
 public SystemMetrics Read()
 {
  var (cpu,engines,memory)=counters.Read();var ram=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};bool available=GlobalMemoryStatusEx(ref ram);
  var gpus=adapters.Select(a=>{
   var matches=memory.Where(m=>m.Name.StartsWith("luid_"+a.Id+"_phys_",StringComparison.OrdinalIgnoreCase)&&m.Value>=0).ToArray();
   ulong? used=matches.Length==0?null:(ulong)matches.Sum(m=>m.Value);
   return new GpuMetric(a.Id,a.Name,GpuAggregation.Load(a.Id,engines),used,a.Dedicated>0?a.Dedicated:null);
  }).ToArray();
  return new(cpuName,cpu,available?ram.Total:null,available?ram.Available:null,gpus);
 }
 public void Dispose()=>counters.Dispose();
 [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,Load;public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended;}
 [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
public sealed class SystemMonitorService : IDisposable
{
 sealed record Options(bool Enabled,bool Gpu,int Interval);
 Options options=new(false,false,1);readonly CancellationTokenSource stop=new();readonly SemaphoreSlim wake=new(0,1);readonly Func<bool,ISystemMetricSource> factory;readonly Task worker;bool disposed;
 public event Action<SystemMetrics>? Changed;
 public Task Completion=>worker;
 public SystemMonitorService(Func<bool,ISystemMetricSource>? factory=null){this.factory=factory??(gpu=>new WindowsMetricSource(gpu));worker=Task.Run(RunAsync);}
 public void Configure(bool enabled,bool gpu,int seconds)
 {
  var next=new Options(enabled,gpu,seconds is 1 or 2 or 5?seconds:1);if(disposed||next==options)return;Volatile.Write(ref options,next);try{wake.Release();}catch(SemaphoreFullException){}
 }
 async Task RunAsync()
 {
  ISystemMetricSource? source=null;bool? gpu=null;
  try{
   while(!stop.IsCancellationRequested){
    var current=Volatile.Read(ref options);
    if(!current.Enabled){source?.Dispose();source=null;gpu=null;await wake.WaitAsync(stop.Token);continue;}
    try{
     if(source==null||gpu!=current.Gpu){source?.Dispose();source=null;source=factory(current.Gpu);gpu=current.Gpu;}
     var sample=source.Read();if(!stop.IsCancellationRequested&&current==Volatile.Read(ref options))Changed?.Invoke(sample);
    }catch(Exception e) when(e is not OperationCanceledException){source?.Dispose();source=null;gpu=null;if(!stop.IsCancellationRequested)Changed?.Invoke(SystemMetrics.Empty);}
    await wake.WaitAsync(TimeSpan.FromSeconds(current.Interval),stop.Token);
   }
  }catch(OperationCanceledException){}finally{source?.Dispose();}
 }
 public void Dispose(){if(disposed)return;disposed=true;Changed=null;stop.Cancel();_ = worker.ContinueWith(_=>{wake.Dispose();stop.Dispose();},TaskScheduler.Default);}
}
