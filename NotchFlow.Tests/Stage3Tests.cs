using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NotchFlow.Configuration;
using NotchFlow.Models;
using NotchFlow.Services;
using NotchFlow.ViewModels;
using NotchFlow.Views;

internal static class Stage3Tests
{
    static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
    static MediaSnapshot Track(DateTimeOffset time)=>new("Track","Artist",null,true,true,true,true,true,TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(300),time,1,"player","track1");
    public static void Core(string root)
    {
        if(Environment.GetEnvironmentVariable("NOTCHFLOW_LIVE_WEATHER")=="1")
        {
            using var live=new WeatherService(Path.Combine(root,"live-weather"));
            var actual=live.FetchAsync("Пятигорск",null).GetAwaiter().GetResult();
            Check(actual.Hours.Count>=24&&actual.Timezone=="Europe/Moscow"&&actual.UtcOffsetSeconds==10800,"Live Open-Meteo request for Pyatigorsk");
            Directory.CreateDirectory(".build");File.WriteAllText(".build/weather-live.json",JsonSerializer.Serialize(actual,new JsonSerializerOptions{WriteIndented=true}));
        }
        var now=DateTimeOffset.UtcNow;var track=Track(now);var timeline=new PlaybackTimeline();timeline.Accept(track,now);
        Check(timeline.Position(now.AddSeconds(35))==125,"Closed timeline advances from timestamp");
        timeline.Accept(track with{Playing=false},now.AddSeconds(35));Check(timeline.Position(now.AddSeconds(85))==125,"Stale pause timestamp freezes projected position");
        timeline.Accept(track,now.AddSeconds(85));Check(timeline.Position(now.AddSeconds(90))==130,"Resume rebases without jump");
        timeline.Accept(track with{Position=TimeSpan.FromSeconds(20),Updated=now.AddSeconds(90)},now.AddSeconds(90));Check(timeline.Position(now.AddSeconds(100))==30,"Seek replaces anchor");
        timeline.Accept(track with{Identity="track2",Position=TimeSpan.Zero,Updated=now.AddSeconds(100),Rate=2},now.AddSeconds(100));Check(timeline.Position(now.AddSeconds(110))==20&&timeline.Position(now.AddHours(1))==300,"Track identity, rate and end clamp");
        timeline.Accept(null,now);Check(timeline.Position(now)==0&&timeline.Duration==0,"Missing session resets timeline");
        var compact=new CompactPlaybackState();Check(compact.ShowClock(now),"No session shows clock");compact.Accept(true,true,now);compact.Accept(true,false,now);compact.Accept(true,false,now.AddSeconds(30));Check(!compact.ShowClock(now.AddSeconds(39))&&compact.ShowClock(now.AddSeconds(40)),"Clock starts at 40 seconds; duplicate pause does not postpone");compact.Accept(true,true,now.AddSeconds(41));Check(!compact.ShowClock(now.AddSeconds(41)),"Resume restores compact music immediately");compact.Accept(true,false,now.AddSeconds(42));Check(!compact.ShowClock(now.AddSeconds(81))&&compact.ShowClock(now.AddSeconds(82)),"Next pause has independent deadline");
        string dir=Path.Combine(root,"personal");var store=new PersonalStore(dir);
        using(var vm=new PersonalViewModel(store)){vm.AddNote();vm.SelectedNote!.Title="Русский / English";vm.SelectedNote.Text="Первая строка\nSecond line";vm.TaskDraft="Купить молоко";vm.AddTask();vm.Tasks[0].IsCompleted=true;Check(vm.Flush(),"Personal data flush succeeds");vm.Tasks[0].IsCompleted=false;vm.Tasks[0].Text="Updated task";Check(vm.Flush(),"Task edit and uncheck saved");}
        var loaded=new PersonalStore(dir).Load();Check(loaded.Notes.Single().Text.Contains('\n')&&loaded.Notes[0].Id!=Guid.Empty&&loaded.Tasks.Single().Text=="Updated task"&&!loaded.Tasks[0].IsCompleted,"Multiline RU/EN notes and task state survive restart");
        using(var pending=new PersonalViewModel(new PersonalStore(dir))){pending.SelectedNote=pending.Notes[0];pending.SelectedNote.Text="Retry after locked file";using(var locked=new FileStream(store.Path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Check(!pending.Flush()&&pending.SelectedNote.Text=="Retry after locked file","Write failure retains pending text");}Check(pending.Flush(),"Retry saves pending changes after write failure");}
        File.WriteAllText(store.Path,"{broken");var recovered=new PersonalStore(dir);Check(recovered.Load().Notes.Count==1&&!recovered.ReadOnly&&Directory.GetFiles(dir,"*.corrupt-*").Length==1,"Corrupt primary recovers backup and preserves evidence");Check(new PersonalStore(dir).Load().Notes.Count==1,"Recovered data survives second restart");
        string corrupt=Path.Combine(root,"corrupt");Directory.CreateDirectory(corrupt);var bad=new PersonalStore(corrupt);File.WriteAllText(bad.Path,"{bad");using(var vm=new PersonalViewModel(bad)){vm.AddNote();Check(!vm.CanEdit&&vm.Notes.Count==0&&File.ReadAllText(bad.Path)=="{bad","Unreadable data is protected from empty overwrite");}
        var future=new PersonalStore(Path.Combine(root,"future"));Directory.CreateDirectory(Path.GetDirectoryName(future.Path)!);File.WriteAllText(future.Path,"{\"Version\":99,\"Notes\":[],\"Tasks\":[]}");future.Load();Check(future.ReadOnly,"Future schema remains read-only");
        using var handler=new ForecastHandler();var service=new WeatherService(Path.Combine(root,"weather"),handler);
        var weather=service.FetchAsync("Пятигорск",null).GetAwaiter().GetResult();Check(weather.Timezone=="Europe/Moscow"&&weather.Temperature==12&&weather.Hours.Count==8&&weather.Hours[0].RainProbability==null,"Weather API parser, timezone and nullable rain");service.SaveCache(weather);Check(service.LoadCache("Пятигорск")?.Temperature==12&&service.LoadCache("Москва")==null,"Weather cache is city-specific");
        using var weatherVm=new WeatherViewModel(service);weatherVm.RefreshAsync(false).GetAwaiter().GetResult();weatherVm.RefreshAsync(false).GetAwaiter().GetResult();Check(handler.Count==1,"Fresh cache avoids repeated requests");handler.Offline=true;weatherVm.RefreshAsync(true).GetAwaiter().GetResult();Check(weatherVm.Temperature=="12°"&&weatherVm.Status.Contains("прошлый"),"Offline error retains last successful forecast");
    }
    public static void Desktop(string root)
    {
        var now=DateTimeOffset.UtcNow;using var media=new MediaService();var vm=new IslandViewModel(media,()=>now,Path.Combine(root,"ui"));vm.AcceptSnapshot(Track(now));vm.SetVisible(false);now=now.AddSeconds(35);Check(vm.Elapsed=="2:05","First opening getter is current before dispatcher tick");
        var window=new IslandWindow(vm,new AppSettings{Animations=false});window.Show();window.Reveal();
        foreach(string tab in new[]{"Music","Notes","Tasks"}){vm.SelectedTab=tab;if(tab=="Notes"){vm.Personal.AddNote();vm.Personal.SelectedNote!.Title="Идеи на сегодня";vm.Personal.SelectedNote.Text="Проверить NotchFlow\nNotes support English too.";}if(tab=="Tasks"){vm.Personal.TaskDraft="Проверить тестовую сборку";vm.Personal.AddTask();vm.Personal.TaskDraft="Готовая задача";vm.Personal.AddTask();vm.Personal.Tasks[1].IsCompleted=true;}Pump(60);Capture(window,tab);}
        for(int wait=0;wait<30 && new PersonalStore(Path.Combine(root,"ui")).Load().Notes.Count==0;wait++)Pump(100);Check(new PersonalStore(Path.Combine(root,"ui")).Load().Notes.Count==1,"Debounced autosave persists without explicit save");
        vm.Personal.SelectedNote!.Text="Last keystroke before close";Pump(50);window.Close();Check(new PersonalStore(Path.Combine(root,"ui")).Load().Notes[0].Text=="Last keystroke before close","Window close flushes final edit");
        using(var personal=new PersonalViewModel(new PersonalStore(Path.Combine(root,"ui")))){personal.SelectedNote=personal.Notes[0];personal.DeleteSelectedNote();personal.RemoveTask(personal.Tasks[0]);}var deleted=new PersonalStore(Path.Combine(root,"ui")).Load();Check(deleted.Notes.Count==0&&deleted.Tasks.Count==1,"Deletion persists after restart");
        using var weather=new WeatherViewModel(new WeatherService(Path.Combine(root,"mock-ui"),new ForecastHandler()));Wait(weather.RefreshAsync(true));var weatherWindow=new Window{Width=400,Height=340,Background=new SolidColorBrush(Color.FromRgb(16,16,18)),Content=new WeatherView{DataContext=weather}};weatherWindow.Show();Pump(50);Capture(weatherWindow,"Weather");weatherWindow.Close();
    }
    static void Wait(Task task){while(!task.IsCompleted)Pump(10);task.GetAwaiter().GetResult();}
    static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void Capture(Window window,string name){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));Directory.CreateDirectory(".build/screenshots");using var file=File.Create(".build/screenshots/stage3-"+name+".png");encoder.Save(file);}
    sealed class ForecastHandler:HttpMessageHandler
    {
        public int Count;public bool Offline;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Count++;if(Offline)throw new HttpRequestException("Offline test");long hour=DateTimeOffset.UtcNow.ToUnixTimeSeconds()/3600*3600;var json=JsonSerializer.Serialize(new{utc_offset_seconds=10800,current=new{time=hour,temperature_2m=12,apparent_temperature=10,relative_humidity_2m=62,is_day=1,weather_code=2,wind_speed_10m=3.5},hourly=new{time=Enumerable.Range(0,8).Select(x=>hour+x*3600).ToArray(),temperature_2m=Enumerable.Repeat(12,8).ToArray(),precipitation_probability=new double?[]{null,10,20,30,20,10,0,0},weather_code=Enumerable.Repeat(2,8).ToArray()}});return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});}
    }
}

