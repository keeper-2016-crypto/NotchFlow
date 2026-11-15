using System;
using System.Globalization;
using System.Windows.Media;
namespace NotchFlow.Controls;
public static class ColorMath
{
 public static Color FromHsv(double h,double s,double v)
 {
  h=((h%360)+360)%360;s=Math.Clamp(s,0,1);v=Math.Clamp(v,0,1);
  double c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c;
  var (r,g,b)=h switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};
  return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
 }
 public static (double H,double S,double V) ToHsv(Color color)
 {
  double r=color.R/255d,g=color.G/255d,b=color.B/255d,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
  double h=d==0?0:max==r?60*((g-b)/d%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);
  return ((h+360)%360,max==0?0:d/max,max);
 }
 public static string Hex(Color c)=>$"#{c.R:X2}{c.G:X2}{c.B:X2}";
 public static bool TryHex(string? text,out Color color)
 {
  color=Colors.White;var s=text?.Trim();
  if(s==null||s.Length!=7||s[0]!='#'||!uint.TryParse(s.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var n))return false;
  color=Color.FromRgb((byte)(n>>16),(byte)(n>>8),(byte)n);return true;
 }
}
