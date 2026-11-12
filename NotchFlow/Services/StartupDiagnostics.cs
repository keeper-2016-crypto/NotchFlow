using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace NotchFlow.Services;
public static class StartupDiagnostics
{
    public static async Task VerifyAsync(Window island, Window settings, string directory)
    {
        Directory.CreateDirectory(directory);
        await Task.Delay(600);
        foreach(var item in new[]{(island,"island"),(settings,"settings")})
        {
            item.Item1.UpdateLayout();
            var image=new RenderTargetBitmap((int)Math.Ceiling(item.Item1.ActualWidth),(int)Math.Ceiling(item.Item1.ActualHeight),96,96,PixelFormats.Pbgra32);
            image.Render(item.Item1);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using var file=File.Create(Path.Combine(directory,item.Item2+".png"));encoder.Save(file);
        }
        if(!island.IsVisible||!settings.IsVisible)throw new InvalidOperationException("Startup windows are not visible");
        File.WriteAllText(Path.Combine(directory,"startup.txt"),$"PASS normal WPF startup, media/audio initialization attempted, both windows created.\nProcess: {Environment.ProcessPath}\nConfig: {new SettingsStore().FilePath}\n");
    }
}
