using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NotchFlow.Controls;
namespace NotchFlow.Views;
public partial class ColorPickerWindow : Window
{
 double hue,saturation,value;bool updating,ready;
 public string SelectedHex{get;private set;}="#FFFFFF";
 public ColorPickerWindow(string initial)
 {
  InitializeComponent();MaxHeight=SystemParameters.WorkArea.Height;
  Color color;try{color=(Color)ColorConverter.ConvertFromString(initial);}catch{color=Colors.White;}
  Before.Background=new SolidColorBrush(color);ready=true;SetColor(color);
  foreach(var (hex,name) in new[]{("#FFFFFF","Белый"),("#FF3333","Красный"),("#A855F7","Фиолетовый"),("#3B82F6","Синий"),("#22D3EE","Голубой"),("#22C55E","Зелёный"),("#EC4899","Розовый"),("#000000","Чёрный")})
  {
   ColorMath.TryHex(hex,out var preset);var button=new Button{Content=new Border{Width=22,Height=22,CornerRadius=new CornerRadius(4),Background=new SolidColorBrush(preset),BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1)},Padding=new Thickness(2),Margin=new Thickness(1)};
   System.Windows.Automation.AutomationProperties.SetName(button,name);button.Click+=(_,_)=>SetColor(preset);Presets.Children.Add(button);
  }
  Closed+=(_,_)=>{if(Palette.IsMouseCaptured)Palette.ReleaseMouseCapture();};
 }
 void SetColor(Color c){(hue,saturation,value)=ColorMath.ToHsv(c);updating=true;Hue.Value=hue;updating=false;Refresh();}
 void Refresh()
 {
  if(!ready)return;updating=true;var color=ColorMath.FromHsv(hue,saturation,value);SelectedHex=ColorMath.Hex(color);
  HueSurface.Background=new SolidColorBrush(ColorMath.FromHsv(hue,1,1));After.Background=new SolidColorBrush(color);HexInput.Text=SelectedHex;Error.Text="";Confirm.IsEnabled=true;updating=false;PlaceMarker();
 }
 void PlaceMarker(){if(!ready)return;Canvas.SetLeft(Marker,saturation*Math.Max(0,Palette.ActualWidth-1)-6);Canvas.SetTop(Marker,(1-value)*Math.Max(0,Palette.ActualHeight-1)-6);}
 void PaletteSizeChanged(object sender,SizeChangedEventArgs e)=>PlaceMarker();
 void Choose(MouseEventArgs e){var p=e.GetPosition(Palette);saturation=Math.Clamp(p.X/Math.Max(1,Palette.ActualWidth-1),0,1);value=1-Math.Clamp(p.Y/Math.Max(1,Palette.ActualHeight-1),0,1);Refresh();}
 void PaletteDown(object sender,MouseButtonEventArgs e){Palette.CaptureMouse();Choose(e);e.Handled=true;}
 void PaletteMove(object sender,MouseEventArgs e){if(Palette.IsMouseCaptured){if(e.LeftButton==MouseButtonState.Pressed)Choose(e);else Palette.ReleaseMouseCapture();}}
 void PaletteUp(object sender,MouseButtonEventArgs e){if(Palette.IsMouseCaptured){Choose(e);Palette.ReleaseMouseCapture();e.Handled=true;}}
 void HueChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(!ready||updating)return;hue=e.NewValue;Refresh();}
 void HexChanged(object sender,TextChangedEventArgs e)
 {
  if(!ready||updating)return;
  if(ColorMath.TryHex(HexInput.Text,out var c))SetColor(c);
  else{Error.Text="Введите цвет в формате #RRGGBB, например #22D3EE.";Confirm.IsEnabled=false;}
 }
 void ConfirmClick(object sender,RoutedEventArgs e){if(ColorMath.TryHex(HexInput.Text,out var color)){SelectedHex=ColorMath.Hex(color);DialogResult=true;}}
}
