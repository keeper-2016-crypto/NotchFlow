using System;
using System.Windows;
using System.Windows.Media.Animation;
namespace NotchFlow.Animations;
public static class Motion
{
 public static void To(FrameworkElement target,DependencyProperty property,double value,int milliseconds) { if(milliseconds==0) {target.BeginAnimation(property,null);target.SetValue(property,value);return;} var animation=new DoubleAnimation(value,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};target.BeginAnimation(property,animation,HandoffBehavior.SnapshotAndReplace); }
}
