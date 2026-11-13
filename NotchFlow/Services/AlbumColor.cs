using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using NotchFlow.Configuration;

namespace NotchFlow.Services;

public sealed record AlbumPalette(Color First, Color Second, Color Third, ImageSource? SoftArtwork);

public static class AlbumColor
{
    static readonly ConditionalWeakTable<ImageSource, AlbumPalette> cache = new();
    static readonly AlbumPalette neutral = new(Color.FromRgb(160,166,180), Color.FromRgb(95,105,125), Color.FromRgb(125,120,140), null);
    public static AlbumPalette Palette(ImageSource? source) => source == null ? neutral : cache.GetValue(source, Extract);
    public static Brush From(ImageSource? source) => new SolidColorBrush(Palette(source).First);
    public static Color Accent(AppSettings settings, AlbumPalette palette) => settings.AccentMode switch
    {
        "Red" => Color.FromRgb(255,59,48), "Purple" => Color.FromRgb(168,85,247),
        "Blue" => Color.FromRgb(59,130,246), "Cyan" => Color.FromRgb(34,211,238),
        "Green" => Color.FromRgb(34,197,94), "Pink" => Color.FromRgb(236,72,153),
        "Custom HEX" => (Color)ColorConverter.ConvertFromString(settings.CustomAccentColor),
        "Dynamic Album Colors" => palette.First, _ => Colors.White
    };
    static AlbumPalette Extract(ImageSource source)
    {
        if(source is not BitmapSource bitmap || bitmap.PixelWidth==0 || bitmap.PixelHeight==0)return neutral;
        try
        {
            var scaled = new TransformedBitmap(bitmap,new ScaleTransform(24d/bitmap.PixelWidth,24d/bitmap.PixelHeight));
            var converted = new FormatConvertedBitmap(scaled,PixelFormats.Bgra32,null,0);
            int stride=converted.PixelWidth*4;
            var bytes=new byte[stride*converted.PixelHeight];
            converted.CopyPixels(bytes,stride,0);
            var totals=new double[3];var red=new double[3];var green=new double[3];var blue=new double[3];
            for(int i=0;i<bytes.Length;i+=4)
            {
                int band=Math.Min(2,(i/4 % converted.PixelWidth)*3/converted.PixelWidth);
                double weight=bytes[i+3]/255d*(.2+(Math.Max(bytes[i],Math.Max(bytes[i+1],bytes[i+2]))-Math.Min(bytes[i],Math.Min(bytes[i+1],bytes[i+2])))/255d);
                totals[band]+=weight;red[band]+=bytes[i+2]*weight;green[band]+=bytes[i+1]*weight;blue[band]+=bytes[i]*weight;
            }
            Color Get(int band) => totals[band]<.01 ? neutral.First : Color.FromRgb(
                (byte)Math.Clamp(red[band]/totals[band]*.72+60,0,255),
                (byte)Math.Clamp(green[band]/totals[band]*.72+60,0,255),
                (byte)Math.Clamp(blue[band]/totals[band]*.72+60,0,255));
            // Blur a small cached artwork surface once, never the live desktop or text.
            var visual=new DrawingVisual { Effect=new BlurEffect {Radius=12,RenderingBias=RenderingBias.Performance} };
            using(var dc=visual.RenderOpen())dc.DrawImage(bitmap,new Rect(-12,-12,120,120));
            var soft=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);
            soft.Render(visual);soft.Freeze();
            return new(Get(0),Get(1),Get(2),soft);
        }
        catch(Exception ex){Log.Error(ex);return neutral;}
    }
}
