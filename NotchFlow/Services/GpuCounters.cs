using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NotchFlow.Models;
namespace NotchFlow.Services;
public sealed record CounterSample(string Name,double Value);
public static class GpuAggregation
{
 static readonly Regex engine=new(@"luid_(0x[0-9a-f]+_0x[0-9a-f]+)_phys_(\d+)_eng_(\d+)_",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
 public static double? Load(string luid,IEnumerable<CounterSample> values)
 {
  var groups=new Dictionary<string,double>();
  foreach(var sample in values){var m=engine.Match(sample.Name);if(!m.Success||!m.Groups[1].Value.Equals(luid,StringComparison.OrdinalIgnoreCase)||!double.IsFinite(sample.Value)||sample.Value<0)continue;
   var key=m.Groups[2].Value+":"+m.Groups[3].Value;groups[key]=groups.GetValueOrDefault(key)+sample.Value;
  }
  // Sum processes on the SAME physical engine, then use the busiest engine. Never add independent engines/adapters.
  return groups.Count==0?null:Math.Clamp(groups.Values.Max(),0,100);
 }
}
internal sealed class PdhCounterSet : IDisposable
{
 IntPtr query,cpu,engine,memory;
 public PdhCounterSet(bool gpu)
 {
  if(PdhOpenQuery(null,IntPtr.Zero,out query)!=0){query=IntPtr.Zero;return;}
  PdhAddEnglishCounter(query,@"\Processor Information(_Total)\% Processor Time",IntPtr.Zero,out cpu);
  if(gpu){PdhAddEnglishCounter(query,@"\GPU Engine(*)\Utilization Percentage",IntPtr.Zero,out engine);PdhAddEnglishCounter(query,@"\GPU Adapter Memory(*)\Dedicated Usage",IntPtr.Zero,out memory);}
  PdhCollectQueryData(query);
 }
 public (double? Cpu,CounterSample[] Engines,CounterSample[] Memory) Read()
 {
  if(query==IntPtr.Zero||PdhCollectQueryData(query)!=0)return(null,System.Array.Empty<CounterSample>(),System.Array.Empty<CounterSample>());
  double? c=null;if(cpu!=IntPtr.Zero&&PdhGetFormattedCounterValue(cpu,0x200,out _,out var value)==0&&value.Status<=1&&double.IsFinite(value.Value))c=Math.Clamp(value.Value,0,100);
  return(c,Array(engine),Array(memory));
 }
 static CounterSample[] Array(IntPtr counter)
 {
  if(counter==IntPtr.Zero)return System.Array.Empty<CounterSample>();uint size=0,count=0;
  PdhGetFormattedCounterArray(counter,0x200|0x8000,ref size,out count,IntPtr.Zero);
  if(size==0||size>16*1024*1024)return System.Array.Empty<CounterSample>();
  IntPtr buffer=Marshal.AllocHGlobal((int)size);
  try{
   if(PdhGetFormattedCounterArray(counter,0x200|0x8000,ref size,out count,buffer)!=0)return System.Array.Empty<CounterSample>();
   var result=new List<CounterSample>();int stride=Marshal.SizeOf<Item>();
   if(count>size/stride)return System.Array.Empty<CounterSample>();
   for(int i=0;i<count;i++){var item=Marshal.PtrToStructure<Item>(buffer+i*stride);if(item.Value.Status<=1&&double.IsFinite(item.Value.Value))result.Add(new(Marshal.PtrToStringUni(item.Name)??"",item.Value.Value));}return result.ToArray();
  }finally{Marshal.FreeHGlobal(buffer);}
 }
 public void Dispose(){if(query!=IntPtr.Zero){PdhCloseQuery(query);query=IntPtr.Zero;}}
 [StructLayout(LayoutKind.Explicit,Size=16)]struct CounterValue { [FieldOffset(0)]public uint Status;[FieldOffset(8)]public double Value; }
 [StructLayout(LayoutKind.Sequential)]struct Item {public IntPtr Name;public CounterValue Value;}
 [DllImport("pdh.dll",CharSet=CharSet.Unicode,EntryPoint="PdhOpenQueryW")]static extern uint PdhOpenQuery(string? source,IntPtr data,out IntPtr query);
 [DllImport("pdh.dll",CharSet=CharSet.Unicode,EntryPoint="PdhAddEnglishCounterW")]static extern uint PdhAddEnglishCounter(IntPtr query,string path,IntPtr data,out IntPtr counter);
 [DllImport("pdh.dll")]static extern uint PdhCollectQueryData(IntPtr query);
 [DllImport("pdh.dll")]static extern uint PdhGetFormattedCounterValue(IntPtr counter,uint format,out uint type,out CounterValue value);
 [DllImport("pdh.dll",EntryPoint="PdhGetFormattedCounterArrayW")]static extern uint PdhGetFormattedCounterArray(IntPtr counter,uint format,ref uint size,out uint count,IntPtr buffer);
 [DllImport("pdh.dll")]static extern uint PdhCloseQuery(IntPtr query);
}
internal static class GpuAdapters
{
 internal sealed record Adapter(string Id,string Name,ulong Dedicated);
 public static Adapter[] Enumerate()
 {
  var iid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");IntPtr factory=IntPtr.Zero;var result=new List<Adapter>();
  try{
   if(CreateDXGIFactory1(ref iid,out factory)<0)return result.ToArray();
   var enumerate=Marshal.GetDelegateForFunctionPointer<EnumAdapter>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory),12*IntPtr.Size));
   for(uint index=0;index<32;index++){
    if(enumerate(factory,index,out var adapter)<0)break;
    try{var desc=Marshal.GetDelegateForFunctionPointer<GetDescription>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(adapter),10*IntPtr.Size));if(desc(adapter,out var d)>=0&&(d.Flags&2)==0)result.Add(new($"0x{d.High:x8}_0x{d.Low:x8}",d.Description,d.Dedicated.ToUInt64()));}
    finally{Marshal.Release(adapter);}
   }
  }finally{if(factory!=IntPtr.Zero)Marshal.Release(factory);}return result.ToArray();
 }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct AdapterDescription {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;
  public uint Vendor,Device,Subsystem,Revision;public UIntPtr Dedicated,System,Shared;public uint Low;public int High;public uint Flags;
 }
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int EnumAdapter(IntPtr self,uint index,out IntPtr adapter);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetDescription(IntPtr self,out AdapterDescription desc);
 [DllImport("dxgi.dll")]static extern int CreateDXGIFactory1(ref Guid iid,out IntPtr factory);
}
