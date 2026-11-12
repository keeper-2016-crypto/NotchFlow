using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NotchFlow.Configuration;
using NotchFlow.Models;
namespace NotchFlow.Services;
public sealed class WeatherService : IDisposable
{
    readonly HttpClient client;
    readonly string cachePath;
    readonly CancellationTokenSource lifetime=new();
    public WeatherService(string? directory=null,HttpMessageHandler? handler=null)
    {
        client=handler==null?new HttpClient():new HttpClient(handler);client.Timeout=TimeSpan.FromSeconds(18);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NotchFlow/0.3");
        cachePath=Path.Combine(directory??Log.DirectoryPath,"weather-cache.json");
    }
    public WeatherSnapshot? LoadCache(string query)
    {
        try{if(!File.Exists(cachePath))return null;var value=AtomicJson.Read(cachePath,DataJsonContext.Default.WeatherSnapshot);return value.Query.Equals(query,StringComparison.OrdinalIgnoreCase)&&value.Hours!=null?value:null;}
        catch(Exception ex){Log.Error(ex);return null;}
    }
    public async Task<WeatherSnapshot> FetchAsync(string query,WeatherSnapshot? previous)
    {
        var ct=lifetime.Token;string city="Пятигорск",timezone="Europe/Moscow";double latitude=44.05,longitude=43.05036;
        if(previous!=null&&previous.Query.Equals(query,StringComparison.OrdinalIgnoreCase)){city=previous.City;timezone=previous.Timezone;latitude=previous.Latitude;longitude=previous.Longitude;}
        else if(!query.Equals("Пятигорск",StringComparison.OrdinalIgnoreCase)&&!query.Equals("Pyatigorsk",StringComparison.OrdinalIgnoreCase))
        {
            using var response=await client.GetAsync("https://geocoding-api.open-meteo.com/v1/search?name="+Uri.EscapeDataString(query)+"&count=1&language=ru&countryCode=RU",ct);
            response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if(!doc.RootElement.TryGetProperty("results",out var results)||results.GetArrayLength()==0)throw new InvalidDataException("Город не найден. Уточните название в настройках.");
            var place=results[0];city=place.GetProperty("name").GetString()??query;latitude=place.GetProperty("latitude").GetDouble();longitude=place.GetProperty("longitude").GetDouble();timezone=place.GetProperty("timezone").GetString()??"Europe/Moscow";
        }
        string url=string.Create(CultureInfo.InvariantCulture,$"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,weather_code,wind_speed_10m&hourly=temperature_2m,precipitation_probability,weather_code&wind_speed_unit=ms&timezone={Uri.EscapeDataString(timezone)}&timeformat=unixtime&forecast_days=2");
        using var forecast=await client.GetAsync(url,ct);forecast.EnsureSuccessStatusCode();
        var snapshot=Parse(await forecast.Content.ReadAsStringAsync(ct),query,city,latitude,longitude,timezone,DateTimeOffset.UtcNow);
        return snapshot;
    }
    public void SaveCache(WeatherSnapshot snapshot)=>AtomicJson.Write(cachePath,snapshot,DataJsonContext.Default.WeatherSnapshot);
    static double? Number(JsonElement element,string name)=>element.TryGetProperty(name,out var n)&&n.ValueKind==JsonValueKind.Number?n.GetDouble():null;
    static double? At(JsonElement element,string name,int index)=>element.TryGetProperty(name,out var a)&&a.ValueKind==JsonValueKind.Array&&index<a.GetArrayLength()&&a[index].ValueKind==JsonValueKind.Number?a[index].GetDouble():null;
    public static WeatherSnapshot Parse(string json,string query,string city,double latitude,double longitude,string timezone,DateTimeOffset now)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;var c=root.GetProperty("current");var hours=root.GetProperty("hourly");
        var result=new WeatherSnapshot{Query=query,City=city,Latitude=latitude,Longitude=longitude,Timezone=timezone,UpdatedAt=now,
            UtcOffsetSeconds=root.GetProperty("utc_offset_seconds").GetInt32(),ObservedAt=c.GetProperty("time").GetInt64(),
            Temperature=Number(c,"temperature_2m")??throw new InvalidDataException("API не вернул температуру"),FeelsLike=Number(c,"apparent_temperature"),
            Wind=Number(c,"wind_speed_10m"),Humidity=Number(c,"relative_humidity_2m"),Code=(int)(Number(c,"weather_code")??-1),IsDay=Number(c,"is_day")==1};
        var times=hours.GetProperty("time");for(int i=0;i<times.GetArrayLength();i++)result.Hours.Add(new WeatherHour{Time=times[i].GetInt64(),Temperature=At(hours,"temperature_2m",i),RainProbability=At(hours,"precipitation_probability",i),Code=(int)(At(hours,"weather_code",i)??-1)});
        return result;
    }
    public void Dispose(){lifetime.Cancel();client.Dispose();lifetime.Dispose();}
}
