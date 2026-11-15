using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using NotchFlow.Views;
namespace NotchFlow.Controls;

public sealed class ColorField : UserControl
{
 public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(string),typeof(ColorField),new FrameworkPropertyMetadata("#FFFFFF",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,Changed));
 public string Value{get=>(string)GetValue(ValueProperty);set=>SetValue(ValueProperty,value);}
 readonly Border swatch=new(){Width=28,Height=24,CornerRadius=new CornerRadius(5),BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1),Margin=new Thickness(0,0,10,0)};
 readonly TextBlock label=new(){VerticalAlignment=VerticalAlignment.Center};
 public ColorField()
 {
  var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(swatch);row.Children.Add(label);row.Children.Add(new TextBlock{Text="Выбрать цвет",Margin=new Thickness(14,0,0,0),VerticalAlignment=VerticalAlignment.Center});
  var button=new Button{Content=row,HorizontalAlignment=HorizontalAlignment.Left};button.Click+=Choose;Content=button;ToolTipService.SetIsEnabled(this,false);Refresh();
 }
 static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)=>((ColorField)d).Refresh();
 void Refresh(){label.Text=Value;try{swatch.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(Value));}catch{swatch.Background=Brushes.Transparent;}}
 void Choose(object sender,RoutedEventArgs e)
 {
  var picker=new ColorPickerWindow(Value){Owner=Window.GetWindow(this)};
  if(picker.ShowDialog()==true){SetCurrentValue(ValueProperty,picker.SelectedHex);GetBindingExpression(ValueProperty)?.UpdateSource();}
 }
}
