using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
namespace NotchFlow.Services;
public sealed class ToggleStateTracker
{
 bool? caps,num;
 public void Reset(){caps=null;num=null;}
 public void Sample(bool? capsValue,bool? numValue,Action<string,bool> changed)
 {
  Observe(ref caps,capsValue,"Caps Lock",changed);Observe(ref num,numValue,"Num Lock",changed);
 }
 static void Observe(ref bool? previous,bool? value,string name,Action<string,bool> changed)
 {var old=previous;previous=value;if(old.HasValue&&value.HasValue&&old!=value)changed(name,value.Value);}
}
// Reads toggle bits only; no hook, text capture or logging of keyboard input.
public sealed class KeyboardToggleService : IDisposable
{
 readonly DispatcherTimer timer=new(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(50)};
 readonly ToggleStateTracker tracker=new();bool caps,num,disposed;
 public event Action<string,bool>? Changed;
 public bool IsRunning=>timer.IsEnabled;
 public KeyboardToggleService(){timer.Tick+=Tick;}
 public void Configure(bool capsEnabled,bool numEnabled)
 {
  if(disposed)return;if(caps==capsEnabled&&num==numEnabled)return;
  caps=capsEnabled;num=numEnabled;timer.Stop();tracker.Reset();Poll();if(caps||num)timer.Start();
 }
 void Tick(object? sender,EventArgs e)=>Poll();
 void Poll()=>tracker.Sample(caps?(GetKeyState(0x14)&1)!=0:null,num?(GetKeyState(0x90)&1)!=0:null,(key,on)=>Changed?.Invoke(key,on));
 [DllImport("user32.dll")]static extern short GetKeyState(int key);
 public void Dispose(){disposed=true;timer.Stop();timer.Tick-=Tick;Changed=null;}
}
