using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NotchFlow.Configuration;
namespace NotchFlow.Views;
public partial class SystemMonitorView : UserControl
{
 public static readonly DependencyProperty HistoryLabelProperty=DependencyProperty.Register(nameof(HistoryLabel),typeof(string),typeof(SystemMonitorView),new PropertyMetadata("1 мин"));
 public string HistoryLabel {get=>(string)GetValue(HistoryLabelProperty);set=>SetValue(HistoryLabelProperty,value);}
 public Brush Accent {get=> (Brush)GetValue(AccentProperty);set=>SetValue(AccentProperty,value);}
 public static readonly DependencyProperty AccentProperty=DependencyProperty.Register(nameof(Accent),typeof(Brush),typeof(SystemMonitorView),new PropertyMetadata(Brushes.White));
 public SystemMonitorView(){InitializeComponent();}
 public void Configure(AppSettings s){HistoryLabel=s.MonitorIntervalSeconds+" мин";CpuCard.Visibility=s.MonitorCpu?Visibility.Visible:Visibility.Collapsed;GpuCard.Visibility=s.MonitorGpu?Visibility.Visible:Visibility.Collapsed;RamCard.Visibility=s.MonitorRam?Visibility.Visible:Visibility.Collapsed;CpuLine.Visibility=GpuLine.Visibility=RamLine.Visibility=s.MonitorGraphs?Visibility.Visible:Visibility.Collapsed;}
}
