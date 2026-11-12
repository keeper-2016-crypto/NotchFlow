using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using NotchFlow.Helpers;
using NotchFlow.Models;
using NotchFlow.Services;
namespace NotchFlow.ViewModels;
public sealed record HourForecast(string Time,string Temperature,string Rain,int Code);
public sealed class WeatherViewModel : ObservableObject,IDisposable
{
    readonly WeatherService service;
    readonly DispatcherTimer refreshTimer=new(){Interval=TimeSpan.FromMinutes(20)};
    string query="Пятигорск",status="Погода ещё не загружена";WeatherSnapshot? snapshot;DateTimeOffset lastAttempt;bool busy,started,disposed;int revision;
    public ObservableCollection<HourForecast> Hours {get;}=new();
    public ICommand Refresh {get;}
    public bool Busy {get=>busy;private set=>Set(ref busy,value);}
    public string Status {get=>status;private set=>Set(ref status,value);}
    public string City=>snapshot?.City??query;
    public string Temperature=>snapshot==null?"—":$"{snapshot.Temperature:0}°";
    public string FeelsLike=>snapshot?.FeelsLike is double n?$"Ощущается как {n:0}°":"Ощущается: —";
    public string Wind=>snapshot?.Wind is double n?$"{n:0.#} м/с":"—";
    public string Humidity=>snapshot?.Humidity is double n?$"{n:0}%":"—";
    public string Rain {get;private set;}="—";
    public int Code=>snapshot?.Code??-1;
    public bool IsDay=>snapshot?.IsDay??true;
    public string Conditions=>Description(Code);
    public string Updated=>snapshot==null?"Источник: Open-Meteo":$"Обновлено {snapshot.UpdatedAt.ToOffset(TimeSpan.FromSeconds(snapshot.UtcOffsetSeconds)):dd.MM HH:mm} · {snapshot.Timezone}";
    public WeatherViewModel(WeatherService service){this.service=service;Refresh=new RelayCommand(async()=>await RefreshAsync(true));refreshTimer.Tick+=RefreshTick;snapshot=service.LoadCache(query);Publish();if(snapshot!=null)Status="Сохранённый прогноз";}
    public void Configure(string city)
    {
        if(query.Equals(city,StringComparison.OrdinalIgnoreCase))return;
        query=city;revision++;lastAttempt=default;snapshot=service.LoadCache(query);Status=snapshot==null?"Город изменён — обновите прогноз":"Сохранённый прогноз";Publish();if(started&&!Busy)_=RefreshAsync(false);
    }
    public void EnsureStarted(){started=true;refreshTimer.Start();_=RefreshAsync(false);}
    async void RefreshTick(object? sender,EventArgs e)=>await RefreshAsync(false);
    public async Task RefreshAsync(bool force)
    {
        if(disposed||Busy)return;
        if(!force&&DateTimeOffset.UtcNow-lastAttempt<TimeSpan.FromMinutes(20))return;
        if(!force&&snapshot!=null&&DateTimeOffset.UtcNow-snapshot.UpdatedAt<TimeSpan.FromMinutes(20)){Status="Актуальный сохранённый прогноз";return;}
        Busy=true;lastAttempt=DateTimeOffset.UtcNow;int requestedRevision=revision;Status="Обновление…";
        try
        {
            var value=await service.FetchAsync(query,snapshot);
            if(disposed||requestedRevision!=revision)return;
            snapshot=value;Publish();Status="";
            try{service.SaveCache(value);}catch(Exception ex){Log.Error(ex);Status="Прогноз получен, но кеш не сохранён";}
        }
        catch(Exception ex)
        {
            if(disposed||requestedRevision!=revision)return;Log.Error(ex);
            Status=(snapshot==null?"Не удалось обновить погоду. ":"Показан прошлый прогноз. ")+(ex is System.IO.InvalidDataException?ex.Message:"Проверьте интернет и повторите попытку.");
        }
        finally{Busy=false;if(!disposed&&requestedRevision!=revision&&started)_=RefreshAsync(false);}
    }
    void Publish()
    {
        Hours.Clear();Rain="—";
        if(snapshot!=null)
        {
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var current=snapshot.Hours.LastOrDefault(h=>h.Time<=now);
            Rain=current?.RainProbability is double p?$"{p:0}%":"—";
            foreach(var h in snapshot.Hours.Where(h=>h.Time>=now).Take(6))Hours.Add(new(DateTimeOffset.FromUnixTimeSeconds(h.Time).ToOffset(TimeSpan.FromSeconds(snapshot.UtcOffsetSeconds)).ToString("HH:mm"),h.Temperature is double t?$"{t:0}°":"—",h.RainProbability is double r?$"{r:0}%":"—",h.Code));
        }
        foreach(var property in new[]{nameof(City),nameof(Temperature),nameof(FeelsLike),nameof(Wind),nameof(Humidity),nameof(Rain),nameof(Code),nameof(IsDay),nameof(Conditions),nameof(Updated)})Raise(property);
    }
    public static string Description(int code)=>code switch{0=>"Ясно",1=>"Преимущественно ясно",2=>"Переменная облачность",3=>"Пасмурно",45 or 48=>"Туман",51 or 53 or 55=>"Морось",56 or 57=>"Ледяная морось",61 or 63 or 65=>"Дождь",66 or 67=>"Ледяной дождь",71 or 73 or 75 or 77=>"Снег",80 or 81 or 82=>"Ливень",85 or 86=>"Снегопад",95 or 96 or 99=>"Гроза",_=>"Нет данных"};
    public void Dispose(){disposed=true;refreshTimer.Stop();refreshTimer.Tick-=RefreshTick;service.Dispose();}
}
