using System;
using System.Windows;
using System.Windows.Media.Animation;
namespace NotchFlow.Animations;
public sealed class CornerRadiusAnimation : AnimationTimeline
{
 public CornerRadius From {get;set;}
 public CornerRadius To {get;set;}
 public override Type TargetPropertyType=>typeof(CornerRadius);
 protected override Freezable CreateInstanceCore()=>new CornerRadiusAnimation{From=From,To=To};
 public override object GetCurrentValue(object defaultOriginValue,object defaultDestinationValue,AnimationClock animationClock) {
  double t=animationClock.CurrentProgress??0;t=1-Math.Pow(1-t,3);
  double Lerp(double a,double b)=>a+(b-a)*t;
  return new CornerRadius(Lerp(From.TopLeft,To.TopLeft),Lerp(From.TopRight,To.TopRight),Lerp(From.BottomRight,To.BottomRight),Lerp(From.BottomLeft,To.BottomLeft));
 }
}
