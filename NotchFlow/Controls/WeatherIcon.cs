using System;
using System.Windows;
using System.Windows.Media;
namespace NotchFlow.Controls;
public sealed class WeatherIcon : FrameworkElement
{
 public static readonly DependencyProperty CodeProperty=DependencyProperty.Register(nameof(Code),typeof(int),typeof(WeatherIcon),new FrameworkPropertyMetadata(-1,FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty IsDayProperty=DependencyProperty.Register(nameof(IsDay),typeof(bool),typeof(WeatherIcon),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
 public int Code{get=>(int)GetValue(CodeProperty);set=>SetValue(CodeProperty,value);}
 public bool IsDay{get=>(bool)GetValue(IsDayProperty);set=>SetValue(IsDayProperty,value);}
 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);dc.PushTransform(new ScaleTransform(ActualWidth/64,ActualHeight/64));
  var white=new SolidColorBrush(Color.FromRgb(230,235,243));var blue=new SolidColorBrush(Color.FromRgb(126,194,255));var yellow=new SolidColorBrush(Color.FromRgb(255,212,109));
  var pen=new Pen(yellow,2.4){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
  if(Code<0){dc.DrawEllipse(null,new Pen(white,2),new Point(32,30),13,13);dc.DrawLine(new Pen(white,2),new Point(28,30),new Point(36,30));dc.Pop();return;}
  if(Code<3)
  {
   if(IsDay){dc.DrawEllipse(yellow,null,new Point(24,23),10,10);for(int i=0;i<8;i++){double a=i*Math.PI/4;dc.DrawLine(pen,new Point(24+Math.Cos(a)*14,23+Math.Sin(a)*14),new Point(24+Math.Cos(a)*18,23+Math.Sin(a)*18));}}
   else dc.DrawGeometry(white,null,Geometry.Parse("M30,6 C13,7 9,25 20,34 C30,41 42,35 45,25 C31,29 22,19 30,6 Z"));
  }
  if(Code>0)
  {
   dc.DrawEllipse(white,null,new Point(24,33),11,11);dc.DrawEllipse(white,null,new Point(36,27),13,13);dc.DrawEllipse(white,null,new Point(48,35),9,9);dc.DrawRoundedRectangle(white,null,new Rect(20,33,30,11),5,5);
   if(Code is 45 or 48){for(int i=0;i<2;i++)dc.DrawLine(new Pen(white,2),new Point(14,49+i*6),new Point(51,49+i*6));}
   else if(Code>=95)dc.DrawGeometry(yellow,null,Geometry.Parse("M32,42 L23,54 31,54 26,63 42,48 33,48 38,42 Z"));
   else if(Code>=71&&Code<=77||Code is 85 or 86){foreach(int x in new[]{22,34,46}){dc.DrawLine(new Pen(white,1.8),new Point(x-3,52),new Point(x+3,52));dc.DrawLine(new Pen(white,1.8),new Point(x,49),new Point(x,55));}}
   else if(Code>=51){foreach(int x in new[]{23,35,47})dc.DrawLine(new Pen(blue,2.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new Point(x,49),new Point(x-3,56));}
  }
  dc.Pop();
 }
}
