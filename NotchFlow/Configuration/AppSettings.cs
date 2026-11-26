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
 public bool FileShelfEnabled {get;set;} = true;
 public bool CapsLockNotifications {get;set;} = true;
 public bool NumLockNotifications {get;set;} = true;
 public int KeyboardNotificationMs {get;set;} = 1800;
 public bool KeyboardNotificationAnimations {get;set;} = true;
 public bool SystemMonitorEnabled {get;set;}
 public bool MonitorCpu {get;set;} = true;
 public bool MonitorGpu {get;set;} = true;
 public bool MonitorRam {get;set;} = true;
 public int MonitorIntervalSeconds {get;set;} = 1;
 public string CompactSystemMetric {get;set;} = "None";
 public bool MonitorGraphs {get;set;} = true;
 public int FloorSensitivity {get;set;} = 65;
 public int FloorSmooth {get;set;} = 50;
 public int FloorOpacity {get;set;} = 45;
 public int FloorHeight {get;set;} = 75;
 public int FloorGlow {get;set;} = 25;
 public int FloorWidth {get;set;} = 65;
 public int FloorBars {get;set;}
 public bool FloorCompact {get;set;}
 public string FloorColor {get;set;}="Accent";
 public string FloorHex {get;set;}="#FF4545";
 public bool AdaptiveMusicWidth {get;set;}=true;
 public bool AnimateMusicWidth {get;set;}=true;
 public int MinimumMusicWidth {get;set;}=80;
 public int MaximumMusicWidth {get;set;}=340;
 public double RadialSensitivity {get;set;}=75;
 public double RadialSmooth {get;set;}=45;
 public double RadialOpacity {get;set;}=85;
 public double RadialLength {get;set;}=100;
 public double RadialThickness {get;set;}=1.5;
 public double RadialGlow {get;set;}=30;
 public double RadialGap {get;set;}=2;
 public double RadialBassAmount {get;set;}=30;
 public int RadialSegments {get;set;}
 public bool RadialMirror {get;set;}=true;
 public bool RadialBass {get;set;}
 public string RadialMode {get;set;}="Both";
 public string RadialColor {get;set;}="Palette";
 public string RadialDistribution {get;set;}="Alternate";
 public int RadialColors {get;set;}
 public double RadialSaturation {get;set;}=110;
 public string RadialHex {get;set;}="#FF4545";
 public AppSettings Clone() => (AppSettings)MemberwiseClone();
 public void Validate() {
  MinimumMusicWidth=Math.Clamp(MinimumMusicWidth,80,240);
  MaximumMusicWidth=Math.Clamp(MaximumMusicWidth,Math.Max(180,MinimumMusicWidth),400);
  RadialSensitivity=double.IsFinite(RadialSensitivity)?Math.Clamp(RadialSensitivity,0,100):75;
  RadialSmooth=double.IsFinite(RadialSmooth)?Math.Clamp(RadialSmooth,0,100):45;
  RadialOpacity=double.IsFinite(RadialOpacity)?Math.Clamp(RadialOpacity,0,100):85;
  RadialLength=double.IsFinite(RadialLength)?Math.Clamp(RadialLength,10,100):80;
  RadialThickness=double.IsFinite(RadialThickness)?Math.Clamp(RadialThickness,0.5,3):1.5;
  RadialGlow=double.IsFinite(RadialGlow)?Math.Clamp(RadialGlow,0,100):30;
  RadialGap=double.IsFinite(RadialGap)?Math.Clamp(RadialGap,1,8):3;
  RadialBassAmount=double.IsFinite(RadialBassAmount)?Math.Clamp(RadialBassAmount,0,100):30;
  if(RadialSegments is not (24 or 32 or 48 or 64))RadialSegments=0;
  if(RadialColors is <2 or >5)RadialColors=0;
  if(RadialDistribution is not ("Alternate" or "Gradient" or "Zones"))RadialDistribution="Alternate";
  RadialSaturation=double.IsFinite(RadialSaturation)?Math.Clamp(RadialSaturation,0,160):110;
  if(RadialMode is not ("Both" or "Compact" or "Expanded"))RadialMode="Both";
  if(RadialColor is not ("Palette" or "Accent" or "Album" or "Custom"))RadialColor="Accent";
  if(!System.Text.RegularExpressions.Regex.IsMatch(RadialHex??"", @"^#[0-9a-fA-F]{6}$"))RadialHex="#FF4545";

  FloorSensitivity=Math.Clamp(FloorSensitivity,0,100);
  FloorSmooth=Math.Clamp(FloorSmooth,0,100);
  FloorOpacity=Math.Clamp(FloorOpacity,0,100);
  FloorHeight=Math.Clamp(FloorHeight,10,100);
  FloorGlow=Math.Clamp(FloorGlow,0,100);
  FloorWidth=Math.Clamp(FloorWidth,20,100);
  if(FloorBars is not (16 or 24 or 32 or 48 or 64))FloorBars=0;
  if(FloorColor is not ("Accent" or "Album" or "Custom"))FloorColor="Accent";
  if(!System.Text.RegularExpressions.Regex.IsMatch(FloorHex??"", @"^#[0-9a-fA-F]{6}$"))FloorHex="#FF4545";

  KeyboardNotificationMs=Math.Clamp(KeyboardNotificationMs,1000,5000);
  if(MonitorIntervalSeconds is not (1 or 2 or 5))MonitorIntervalSeconds=1;
  if(CompactSystemMetric is not ("None" or "CPU" or "GPU" or "RAM" or "Auto"))CompactSystemMetric="None";
  if(!double.IsFinite(GlassOpacity))throw new ArgumentException("Некорректная плотность тонировки стекла.");
  GlassOpacity=Math.Clamp(GlassOpacity,0,1);
  if(GlassTint is not ("Dark" or "Neutral" or "Album Color" or "Custom"))GlassTint="Dark";
  if(GlassReflection is not ("Off" or "Low" or "Medium" or "High"))GlassReflection="Medium";
  if(GlassCustomTint==null||!System.Text.RegularExpressions.Regex.IsMatch(GlassCustomTint,@"\A#[0-9a-fA-F]{6}\z"))throw new ArgumentException("Укажите цвет стекла в формате HEX, например #121216.");
  if(!double.IsFinite(AudioSensitivity))throw new ArgumentException("Некорректная чувствительность к звуку.");AudioSensitivity=Math.Clamp(AudioSensitivity,.25,4);
  if(MediaVisualizer is not ("Classic" or "Equalizer" or "Spectrum Floor" or "Radial Pulse"))MediaVisualizer="Classic";
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




