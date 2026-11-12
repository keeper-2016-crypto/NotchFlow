using System;
using System.Collections.Generic;
namespace NotchFlow.Models;
public sealed class WeatherHour
{
    public long Time {get;set;}
    public double? Temperature {get;set;}
    public double? RainProbability {get;set;}
    public int Code {get;set;}
}
public sealed class WeatherSnapshot
{
    public string Query {get;set;}="Пятигорск";
    public string City {get;set;}="Пятигорск";
    public double Latitude {get;set;}=44.05;
    public double Longitude {get;set;}=43.05036;
    public string Timezone {get;set;}="Europe/Moscow";
    public int UtcOffsetSeconds {get;set;}=10800;
    public DateTimeOffset UpdatedAt {get;set;}
    public long ObservedAt {get;set;}
    public double Temperature {get;set;}
    public double? FeelsLike {get;set;}
    public double? Wind {get;set;}
    public double? Humidity {get;set;}
    public int Code {get;set;}
    public bool IsDay {get;set;}
    public List<WeatherHour> Hours {get;set;}=new();
}
