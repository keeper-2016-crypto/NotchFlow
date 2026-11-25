using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using NotchFlow.Helpers;
using NotchFlow.Models;
namespace NotchFlow.ViewModels;
public sealed class SystemMonitorViewModel : ObservableObject
{
 SystemMetrics metrics=SystemMetrics.Empty;readonly Queue<double?> history=new(),gpuHistory=new(),ramHistory=new();int rotation;
 public SystemMetrics Metrics=>metrics;
 public string CpuName=>metrics.CpuName;
 public string CpuText=>Percent(metrics.CpuLoad);
 public string RamText=>Percent(metrics.RamLoad);
 public string RamDetail=>metrics.TotalRam is ulong total&&metrics.AvailableRam is ulong free?$"Занято {Size(total-Math.Min(total,free))} из {Size(total)}\nДоступно {Size(free)}":"Недоступно";
 public string GpuText=>Percent(metrics.GpuLoad);
 public string GpuDetail=>metrics.Gpus.Count==0?"Нет доступных адаптеров / счётчиков":string.Join("\n",metrics.Gpus.Select(g=>$"{g.Name}\n{Percent(g.Load)} · Видеопамять: {(g.DedicatedUsed is ulong used?Size(used):"Недоступно")}{(g.DedicatedTotal is ulong total?" / "+Size(total):"")}"));
 public double CpuValue=>metrics.CpuLoad??0;public double RamValue=>metrics.RamLoad??0;public double GpuValue=>metrics.GpuLoad??0;
 public double?[] History=>history.ToArray();
 public Geometry CpuGraph=>Graph(history);
 public Geometry GpuGraph=>Graph(gpuHistory);
 public Geometry RamGraph=>Graph(ramHistory);

 public Geometry CpuFill=>Graph(history,true);
 public Geometry GpuFill=>Graph(gpuHistory,true);
 public Geometry RamFill=>Graph(ramHistory,true);
 static Geometry Graph(Queue<double?> history,bool fill=false)
 {
  var geometry=new StreamGeometry();
  using(var context=geometry.Open()){
   int index=60-history.Count;
   bool drawing=false;
   double lastX=0;
   foreach(var sample in history){
    double x=index++*104d/59;
    if(sample is not double load){
     if(drawing && fill)context.LineTo(new Point(lastX,50),false,false);
     drawing=false;
     continue;
    }
    var point=new Point(x,50-Math.Clamp(load,0,100)*.5);
    if(!drawing){
     context.BeginFigure(fill?new Point(x,50):point,fill,fill);
     if(fill)context.LineTo(point,false,false);
     drawing=true;
    }else context.LineTo(point,true,false);
    lastX=x;
   }
   if(drawing && fill)context.LineTo(new Point(lastX,50),false,false);
  }
  geometry.Freeze();
  return geometry;
 }
 public void Accept(SystemMetrics value){metrics=value;if(history.Count==60){history.Dequeue();gpuHistory.Dequeue();ramHistory.Dequeue();}history.Enqueue(value.CpuLoad);gpuHistory.Enqueue(value.GpuLoad);ramHistory.Enqueue(value.RamLoad);rotation++;foreach(var p in new[]{nameof(Metrics),nameof(CpuName),nameof(CpuText),nameof(RamText),nameof(RamDetail),nameof(GpuText),nameof(GpuDetail),nameof(CpuValue),nameof(RamValue),nameof(GpuValue),nameof(CpuGraph),nameof(GpuGraph),nameof(RamGraph),nameof(CpuFill),nameof(GpuFill),nameof(RamFill)})Raise(p);}
 public string Compact(string mode,bool cpu,bool gpu,bool ram)
 {
  var available=new List<string>();if(cpu)available.Add("CPU");if(gpu)available.Add("GPU");if(ram)available.Add("RAM");
  if(mode=="Auto")mode=available.Count==0?"None":available[(rotation/5)%available.Count];
  return mode switch{"CPU" when cpu=>"CPU  "+CpuText,"GPU" when gpu=>"GPU  "+GpuText,"RAM" when ram=>"RAM  "+RamText,_=>""};
 }
 static string Percent(double? value)=>value is double v?$"{v:0}%":"Недоступно";
 static string Size(ulong bytes)=>$"{bytes/1073741824d:0.#} ГБ";
}
