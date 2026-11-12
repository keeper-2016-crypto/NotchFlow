using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace NotchFlow.Services;
public static class AlbumColor
{
    public static Brush From(ImageSource? source)
    {
        if (source is not BitmapSource bitmap) return new SolidColorBrush(Color.FromRgb(200,200,202));
        try
        {
            var scaled = new TransformedBitmap(bitmap, new ScaleTransform(16d / bitmap.PixelWidth, 16d / bitmap.PixelHeight));
            var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            int stride = converted.PixelWidth * 4; var bytes = new byte[stride * converted.PixelHeight]; converted.CopyPixels(bytes, stride, 0);
            double red=0, green=0, blue=0, total=0;
            for (int i=0;i<bytes.Length;i+=4)
            {
                double max=Math.Max(bytes[i],Math.Max(bytes[i+1],bytes[i+2])), min=Math.Min(bytes[i],Math.Min(bytes[i+1],bytes[i+2]));
                double weight=(max-min)/255d * bytes[i+3]/255d;
                red+=bytes[i+2]*weight;green+=bytes[i+1]*weight;blue+=bytes[i]*weight;total+=weight;
            }
            if(total<.01) return new SolidColorBrush(Color.FromRgb(200,200,202));
            byte Lift(double v)=>(byte)Math.Clamp(v/total*.6+100,0,255);
            var result=new SolidColorBrush(Color.FromRgb(Lift(red),Lift(green),Lift(blue)));result.Freeze();return result;
        }
        catch (Exception ex) {Log.Error(ex);return Brushes.White;}
    }
}
