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
 public bool AutoCheckUpdates {get;set;} = true;
 public bool CheckUpdatesAtStartup {get;set;} = true;
 public bool AudioAnalysisEnabled {get;set;}
 public double AudioSensitivity {get;set;} = 1;
 public string MediaVisualizer {get;set;} = "Classic";
 public string AccentMode {get;set;} = "Default White";
 public string CustomAccentColor {get;set;} = "#FFFFFF";
 public string AlbumArtStyle {get;set;} = "Clean";
 public bool GlassBackdropBlur {get;set;} = true;
 public double GlassOpacity {get;set;} = .88;
 public string GlassTint {get;set;} = "Dark";
 public string GlassCustomTint {get;set;} = "#121216";
 public string GlassReflection {get;set;} = "Medium";
 public bool GlassEdgeHighlight {get;set;} = true;
 public bool GlassDynamicLighting {get;set;} = true;
 public string BackgroundEffect {get;set;} = "Solid";
 public double TintIntensity {get;set;} = .65;
 public double BorderGlow {get;set;} = .25;
 public bool ReduceMotion {get;set;}
 public AppSettings Clone() => (AppSettings)MemberwiseClone();
 public void Validate() {
  if(!double.IsFinite(GlassOpacity))throw new ArgumentException("Некорректная плотность тонировки стекла.");
  GlassOpacity=Math.Clamp(GlassOpacity,0,1);
  if(GlassTint is not ("Dark" or "Neutral" or "Album Color" or "Custom"))GlassTint="Dark";
  if(GlassReflection is not ("Off" or "Low" or "Medium" or "High"))GlassReflection="Medium";
  if(GlassCustomTint==null||!System.Text.RegularExpressions.Regex.IsMatch(GlassCustomTint,@"\A#[0-9a-fA-F]{6}\z"))throw new ArgumentException("Укажите цвет стекла в формате HEX, например #121216.");
  if(!double.IsFinite(AudioSensitivity))throw new ArgumentException("Некорректная чувствительность к звуку.");AudioSensitivity=Math.Clamp(AudioSensitivity,.25,4);
  if(MediaVisualizer is not ("Classic" or "Equalizer"))MediaVisualizer="Classic";
  if(AccentMode is not ("Default White" or "Red" or "Purple" or "Blue" or "Cyan" or "Green" or "Pink" or "Custom HEX" or "Dynamic Album Colors"))AccentMode="Default White";
  if(AlbumArtStyle is not ("Clean" or "Blurred" or "Gradient" or "Blur + Gradient"))AlbumArtStyle="Clean";
  if(BackgroundEffect is not ("Solid" or "Acrylic Glass" or "Liquid Glass" or "Album Gradient"))BackgroundEffect="Solid";
  if(!double.IsFinite(TintIntensity)||!double.IsFinite(BorderGlow))throw new ArgumentException("Некорректная интенсивность эффекта.");
  TintIntensity=Math.Clamp(TintIntensity,0,1);BorderGlow=Math.Clamp(BorderGlow,0,1);
  if(CustomAccentColor==null || !System.Text.RegularExpressions.Regex.IsMatch(CustomAccentColor, @"\A#[0-9a-fA-F]{6}\z"))throw new ArgumentException("Укажите акцентный цвет в формате HEX, например #22D3EE.");
  WeatherCity=string.IsNullOrWhiteSpace(WeatherCity)?"Пятигорск":WeatherCity.Trim();if(WeatherCity.Length>120)throw new ArgumentException("Название города слишком длинное.");
  if (!double.IsFinite(Opacity) || !double.IsFinite(Scale) || !double.IsFinite(OffsetX) || !double.IsFinite(OffsetY) || !double.IsFinite(CornerRadius)) throw new ArgumentException("Некорректные числовые настройки.");
  Opacity=Math.Clamp(Opacity,.3,1); Scale=Math.Clamp(Scale,.8,1.8); CornerRadius=Math.Clamp(CornerRadius,8,40); AnimationMs=Math.Clamp(AnimationMs,150,300);
  OffsetX=Math.Clamp(OffsetX,-10000,10000); OffsetY=Math.Clamp(OffsetY,0,10000);
  if (Position is not ("Center" or "Left" or "Right")) Position="Center";
  if(string.IsNullOrWhiteSpace(CapsuleColor))throw new ArgumentException("Цвет капсулы не указан.");
  try {_ = (Color)ColorConverter.ConvertFromString(CapsuleColor);}catch(Exception ex) when(ex is FormatException or NotSupportedException){throw new ArgumentException("Некорректный цвет капсулы.",ex);}
 }
}




