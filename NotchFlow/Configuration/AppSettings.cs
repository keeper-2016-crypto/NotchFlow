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
 public bool AudioAnalysisEnabled {get;set;}
 public double AudioSensitivity {get;set;} = 1;
 public string RibbonColorMode {get;set;} = "Pink / Purple / Blue";
 public string RibbonGlow {get;set;} = "Medium";
 public string RibbonSensitivity {get;set;} = "Normal";
 public string RibbonThickness {get;set;} = "Normal";
 public string RibbonAnimation {get;set;} = "Audio Reactive";
 public string MediaVisualizer {get;set;} = "Classic";
 public string AccentMode {get;set;} = "Default White";
 public string CustomAccentColor {get;set;} = "#FFFFFF";
 public string AlbumArtStyle {get;set;} = "Clean";
 public string BackgroundEffect {get;set;} = "Solid";
 public double TintIntensity {get;set;} = .65;
 public double BorderGlow {get;set;} = .25;
 public string AnimationIntensity {get;set;} = "Medium";
 public bool ReduceMotion {get;set;}
 public bool AnimateOnlyWhilePlaying {get;set;} = true;
 public AppSettings Clone() => (AppSettings)MemberwiseClone();
 public void Validate() {
  if(RibbonColorMode is not ("Pink / Purple / Blue" or "Album Colors" or "Custom Color"))RibbonColorMode="Pink / Purple / Blue";
  if(RibbonGlow is not ("Low" or "Medium" or "High"))RibbonGlow="Medium";
  if(RibbonSensitivity is not ("Low" or "Normal" or "High"))RibbonSensitivity="Normal";
  if(RibbonThickness is not ("Thin" or "Normal" or "Bold"))RibbonThickness="Normal";
  if(RibbonAnimation is not ("Audio Reactive" or "Static" or "Disabled"))RibbonAnimation="Audio Reactive";
  if(!double.IsFinite(AudioSensitivity))throw new ArgumentException("Invalid audio sensitivity.");AudioSensitivity=Math.Clamp(AudioSensitivity,.25,4);
  if(MediaVisualizer is not ("Classic" or "Album Glow" or "Breathing Island" or "Equalizer" or "Neon Ribbon"))MediaVisualizer="Classic";
  if(AccentMode is not ("Default White" or "Red" or "Purple" or "Blue" or "Cyan" or "Green" or "Pink" or "Custom HEX" or "Dynamic Album Colors"))AccentMode="Default White";
  if(AlbumArtStyle is not ("Clean" or "Blurred" or "Gradient" or "Blur + Gradient"))AlbumArtStyle="Clean";
  if(BackgroundEffect is not ("Solid" or "Acrylic Glass" or "Liquid Glass" or "Album Gradient"))BackgroundEffect="Solid";
  if(AnimationIntensity is not ("Low" or "Medium" or "High"))AnimationIntensity="Medium";
  if(!double.IsFinite(TintIntensity)||!double.IsFinite(BorderGlow))throw new ArgumentException("Invalid effect intensity.");
  TintIntensity=Math.Clamp(TintIntensity,0,1);BorderGlow=Math.Clamp(BorderGlow,0,1);
  if(CustomAccentColor==null || !System.Text.RegularExpressions.Regex.IsMatch(CustomAccentColor, @"\A#[0-9a-fA-F]{6}\z"))throw new ArgumentException("Use a HEX color such as #22D3EE.");
  WeatherCity=string.IsNullOrWhiteSpace(WeatherCity)?"Пятигорск":WeatherCity.Trim();if(WeatherCity.Length>120)throw new ArgumentException("Название города слишком длинное.");
  if (!double.IsFinite(Opacity) || !double.IsFinite(Scale) || !double.IsFinite(OffsetX) || !double.IsFinite(OffsetY) || !double.IsFinite(CornerRadius)) throw new ArgumentException("Некорректные числовые настройки.");
  Opacity=Math.Clamp(Opacity,.3,1); Scale=Math.Clamp(Scale,.8,1.8); CornerRadius=Math.Clamp(CornerRadius,8,40); AnimationMs=Math.Clamp(AnimationMs,150,300);
  OffsetX=Math.Clamp(OffsetX,-10000,10000); OffsetY=Math.Clamp(OffsetY,0,10000);
  if (Position is not ("Center" or "Left" or "Right")) Position="Center";
  if(string.IsNullOrWhiteSpace(CapsuleColor))throw new ArgumentException("Цвет капсулы не указан.");
  try {_ = (Color)ColorConverter.ConvertFromString(CapsuleColor);}catch(Exception ex) when(ex is FormatException or NotSupportedException){throw new ArgumentException("Некорректный цвет капсулы.",ex);}
 }
}




