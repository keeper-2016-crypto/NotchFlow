using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NotchFlow.Services;

internal static class RadialPalette
{
    static readonly ConditionalWeakTable<BitmapSource, Color[]> cache = new();
    public static Color[] Read(ImageSource? art) => art is BitmapSource bitmap
        ? cache.GetValue(bitmap, Extract) : Array.Empty<Color>();

    static Color[] Extract(BitmapSource art)
    {
        var small = new TransformedBitmap(art, new ScaleTransform(40d/art.PixelWidth, 40d/art.PixelHeight));
        var image = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(bytes, image.PixelWidth * 4, 0);
        var bins = new Dictionary<int,(int Count,int R,int G,int B)>();
        for (int i=0; i<bytes.Length; i+=4)
        {
            int r=bytes[i+2], g=bytes[i+1], b=bytes[i];
            if (bytes[i+3]<128 || Math.Max(r,Math.Max(g,b))<28) continue;
            int key=((r>>5)<<6)|((g>>5)<<3)|(b>>5);
            var bin=bins.GetValueOrDefault(key);
            bins[key]=(bin.Count+1,bin.R+r,bin.G+g,bin.B+b);
        }
        var ranked=bins.Values.Select(v=>{
            var c=Color.FromRgb((byte)(v.R/v.Count),(byte)(v.G/v.Count),(byte)(v.B/v.Count));
            int chroma=Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B));
            return (Color:c, Score:Math.Sqrt(v.Count)*(.035+chroma/255d));
        }).OrderByDescending(v=>v.Score);
        var colors=new List<Color>();
        foreach(var v in ranked)
        {
            if(colors.Any(c=>Math.Abs(c.R-v.Color.R)+Math.Abs(c.G-v.Color.G)+Math.Abs(c.B-v.Color.B)<100))continue;
            colors.Add(v.Color);
            if(colors.Count==5)break;
        }
        return colors.ToArray();
    }

    public static Color Mix(Color a,Color b,double t)=>Color.FromRgb(
        (byte)Math.Round(a.R+(b.R-a.R)*t),(byte)Math.Round(a.G+(b.G-a.G)*t),(byte)Math.Round(a.B+(b.B-a.B)*t));

    public static Color Tone(Color c,double saturation)
    {
        double gray=.21*c.R+.72*c.G+.07*c.B;
        double boost=Math.Max(1,168d/Math.Max(1d,Math.Max(c.R,Math.Max(c.G,c.B))));
        byte Channel(byte x)=>(byte)Math.Clamp((gray+(x-gray)*saturation)*boost,0,255);
        return Color.FromRgb(Channel(c.R),Channel(c.G),Channel(c.B));
    }
}
