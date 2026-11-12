using System;
using System.Windows.Media;
namespace NotchFlow.Configuration;
public sealed class AppSettings
{
 public double Opacity {get;set;} = 1;
 public string CapsuleColor {get;set;} = "#0B0B0C";
 public double Scale {get;set;} = 1;
 public double CornerRadius {get;set;} = 36;
 public bool AttachToTop {get;set;}
 public bool Shadow {get;set;} = true;
 public bool Animations {get;set;} = true;
 public int AnimationMs {get;set;} = 220;
 public bool AlwaysOnTop {get;set;} = true;
 public bool CollapseOnLeave {get;set;} = true;
 public bool StartCollapsed {get;set;} = true;
 public bool MediaEnabled {get;set;} = true;
 public bool VolumeEnabled {get;set;} = true;
 public string Monitor {get;set;} = "";
 public string Position {get;set;} = "Center";
 public double OffsetX {get;set;}
 public double OffsetY {get;set;} = 6;
 public string WeatherCity {get;set;}="Пятигорск";
 public bool AutoStart {get;set;}
 public AppSettings Clone() => (AppSettings)MemberwiseClone();
 public void Validate() {
  WeatherCity=string.IsNullOrWhiteSpace(WeatherCity)?"Пятигорск":WeatherCity.Trim();if(WeatherCity.Length>120)throw new ArgumentException("Название города слишком длинное.");
  if (!double.IsFinite(Opacity) || !double.IsFinite(Scale) || !double.IsFinite(OffsetX) || !double.IsFinite(OffsetY) || !double.IsFinite(CornerRadius)) throw new ArgumentException("Некорректные числовые настройки.");
  Opacity=Math.Clamp(Opacity,.3,1); Scale=Math.Clamp(Scale,.8,1.8); CornerRadius=Math.Clamp(CornerRadius,8,40); AnimationMs=Math.Clamp(AnimationMs,150,300);
  OffsetX=Math.Clamp(OffsetX,-10000,10000); OffsetY=Math.Clamp(OffsetY,0,10000);
  if (Position is not ("Center" or "Left" or "Right")) Position="Center";
  if(string.IsNullOrWhiteSpace(CapsuleColor))throw new ArgumentException("Цвет капсулы не указан.");
  try {_ = (Color)ColorConverter.ConvertFromString(CapsuleColor);}catch(Exception ex) when(ex is FormatException or NotSupportedException){throw new ArgumentException("Некорректный цвет капсулы.",ex);}
 }
}




