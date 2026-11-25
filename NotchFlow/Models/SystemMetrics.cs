using System;
using System.Collections.Generic;
using System.Linq;
namespace NotchFlow.Models;
public sealed record GpuMetric(string Id,string Name,double? Load,ulong? DedicatedUsed,ulong? DedicatedTotal);
public sealed record SystemMetrics(string CpuName,double? CpuLoad,ulong? TotalRam,ulong? AvailableRam,IReadOnlyList<GpuMetric> Gpus)
{
 public double? RamLoad=>TotalRam is >0&&AvailableRam is ulong available?Math.Clamp(100d*(TotalRam.Value- Math.Min(TotalRam.Value,available))/TotalRam.Value,0,100):null;
 public double? GpuLoad=>Gpus.Any(g=>g.Load.HasValue)?Gpus.Where(g=>g.Load.HasValue).Max(g=>g.Load):null;
 public static SystemMetrics Empty {get;}=new("Процессор",null,null,null,Array.Empty<GpuMetric>());
}
