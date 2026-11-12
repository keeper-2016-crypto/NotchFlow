using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace NotchFlow.Controls;
public sealed class IslandSurface : Border
{
    public static readonly DependencyProperty IsNotchProperty = DependencyProperty.Register(nameof(IsNotch), typeof(bool), typeof(IslandSurface), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsNotch {get=>(bool)GetValue(IsNotchProperty);set=>SetValue(IsNotchProperty,value);}
    public Geometry Outline(double width,double height)
    {
        width=Math.Max(0,width);height=Math.Max(0,height);
        double radius=Math.Min(CornerRadius.TopLeft,Math.Min(height/2,width/2));
        if(!IsNotch)return new RectangleGeometry(new Rect(0,0,width,height),radius,radius);
        double inset=Math.Min(10,width/4),left=inset,right=width-inset;
        radius=Math.Min(radius,(right-left)/2);
        var shape=new StreamGeometry();
        using(var g=shape.Open())
        {
            g.BeginFigure(new Point(0,0),true,true);
            g.QuadraticBezierTo(new Point(left,0),new Point(left,inset),true,false);
            g.LineTo(new Point(left,height-radius),true,false);
            g.QuadraticBezierTo(new Point(left,height),new Point(left+radius,height),true,false);
            g.LineTo(new Point(right-radius,height),true,false);
            g.QuadraticBezierTo(new Point(right,height),new Point(right,height-radius),true,false);
            g.LineTo(new Point(right,inset),true,false);
            g.QuadraticBezierTo(new Point(right,0),new Point(width,0),true,false);
        }
        shape.Freeze();return shape;
    }
    protected override void OnRender(DrawingContext dc)
    {
        var shape=Outline(ActualWidth,ActualHeight);
        dc.DrawGeometry(Background,BorderBrush==null?null:new Pen(BorderBrush,.5),shape);
        if(Child!=null)Child.Clip=Outline(Child.RenderSize.Width,Child.RenderSize.Height);
    }
}
