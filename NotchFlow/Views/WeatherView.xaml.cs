using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
namespace NotchFlow.Views;
public partial class WeatherView : UserControl
{
 public WeatherView()=>InitializeComponent();
 void OpenSource(object sender,RequestNavigateEventArgs e){try{Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});}catch(System.Exception ex){Services.Log.Error(ex);}e.Handled=true;}
}
