using System.IO;
using NotchFlow.Models;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Configuration;

internal static class DesktopFeaturesTests
{
    static void Check(bool ok,string name) { if(!ok) throw new InvalidOperationException(name); Console.WriteLine("PASS: "+name); }
    public static async Task Run(string root)
    {
        string dir=Path.Combine(root,"shelf-test");
        Directory.CreateDirectory(dir);
        string file=Path.Combine(dir,"original.txt");
        File.WriteAllText(file,"original");
        var service=new FileShelfService(dir);
        using var shelf=new FileShelfViewModel(service);
        await shelf.LoadAsync();
        await shelf.AddAsync(new[]{file,file,dir,Path.Combine(dir,"missing.txt")});
        Check(shelf.Items.Count==3 && shelf.Items.Single(i=>i.Path==file).Size==8,"Shelf deduplicates and reads real metadata");
        Check(!shelf.Items.Last().Available && shelf.Items[1].Type=="Папка","Shelf handles unavailable files and folders");
        Check((await new FileShelfService(dir).LoadAsync()).Length==3,"Shelf survives restart");
        await shelf.RemoveAsync(file); await shelf.ClearAsync();
        Check(File.ReadAllText(file)=="original" && (await service.LoadAsync()).Length==0,"Removing and clearing references preserves originals");
        var tracker=new ToggleStateTracker(); var events=new List<string>();
        foreach(bool state in new[]{false,true,true,false,true}) tracker.Sample(state,null,(k,v)=>events.Add(k+v));
        Check(events.SequenceEqual(new[]{"Caps LockTrue","Caps LockFalse","Caps LockTrue"}),"Rapid toggles ignore baseline and duplicate states");
        using var keyboard=new KeyboardToggleService();keyboard.Configure(true,false);keyboard.Configure(false,false);
        Check(!keyboard.IsRunning,"Disabled keyboard tracker releases timer");
        string id="0x00000000_0x00000042";
        CounterSample Sample(int pid,int engine,double n)=>new($"pid_{pid}_luid_{id}_phys_0_eng_{engine}_engtype_3D",n);
        Check(GpuAggregation.Load(id,new[]{Sample(1,0,12),Sample(2,0,25),Sample(1,1,30)})==37,"GPU reports busiest engine across processes");
        var vm=new SystemMonitorViewModel();
        for(int i=0;i<61;i++)vm.Accept(new("CPU",i,100,25,Array.Empty<GpuMetric>()));
        Check(vm.History.Length==60 && vm.History[0]==1 && vm.RamText=="75%" && vm.GpuText=="Недоступно","Bounded history, real RAM and missing GPU states");
        var settings=new SettingsStore(dir);settings.Save(new(){FileShelfEnabled=false,MonitorIntervalSeconds=5,CompactSystemMetric="RAM",CapsLockNotifications=false});
        var restored=settings.Load();Check(!restored.FileShelfEnabled && !restored.CapsLockNotifications && restored.CompactSystemMetric=="RAM" && restored.MonitorIntervalSeconds==5,"Feature preferences survive restart");
        using var source=new WindowsMetricSource(true); var metrics=source.Read();
        Check(metrics.TotalRam>0 && metrics.AvailableRam<=metrics.TotalRam,"Native Windows memory and adapter enumeration");
    }
}
