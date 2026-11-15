using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using NotchFlow.Services;
using NotchFlow.Views;
internal static class UpdateUiTests
{
 public static void Run(string root)
 {
  using var key=RSA.Create(2048);var origin=new Uri("https://195.78.49.209:8443/notchflow/version.json");
  var manifest=new UpdateManifest("0.3.6","stable","win-x64","https://195.78.49.209:8443/notchflow/releases/test.exe",new string('0',64),12345,"Тестовый подписанный выпуск\n• Проверка интерфейса\n• Улучшения обновления");
  var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
  var release=new UpdateVerifier(key.ExportSubjectPublicKeyInfoPem(),origin).Verify(bytes,key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
  using var service=new UpdateService(UpdateService.CreateClient(),key.ExportSubjectPublicKeyInfoPem(),origin,root,new Version(0,3,5),true);
  using var coordinator=new UpdateCoordinator(service,()=>new(),System.IO.Path.Combine(root,"update-ui-state.json"));int installed=0;
  var window=new UpdateWindow(coordinator,release,(_,_)=>{installed++;return Task.CompletedTask;});window.Show();window.UpdateLayout();
  if(!((TextBlock)window.FindName("VersionsText")).Text.Contains("0.3.5")||!((TextBlock)window.FindName("VersionsText")).Text.Contains("0.3.6"))throw new Exception("Update version labels");
  var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create(System.IO.Path.Combine(Environment.CurrentDirectory,".build","update-window.png")))encoder.Save(file);
  ((Button)window.FindName("LaterButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(installed!=0||window.IsVisible)throw new Exception("Deferral unexpectedly installs");Console.WriteLine("PASS: UpdateLaterNeverInstalls");
 }
}
