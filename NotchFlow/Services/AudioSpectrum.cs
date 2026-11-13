using System;
using NAudio.Dsp;

namespace NotchFlow.Services;

public sealed record AudioFrame(float Rms,float Bass,float Mid,float Treble,float Peak,float Onset,float[] Bands)
{
    public static AudioFrame Combine(AudioFrame left,AudioFrame right)
    {
        var bands=new float[16];
        for(int i=0;i<bands.Length;i++)bands[i]=Math.Max(left.Bands[i],right.Bands[i]);
        return new(Math.Max(left.Rms,right.Rms),Math.Max(left.Bass,right.Bass),Math.Max(left.Mid,right.Mid),Math.Max(left.Treble,right.Treble),Math.Max(left.Peak,right.Peak),Math.Max(left.Onset,right.Onset),bands);
    }
    public AudioFrame Decay()
    {
        if(ReferenceEquals(this,Silent))return Silent;
        var bands=new float[16];float maximum=0;
        for(int i=0;i<bands.Length;i++){bands[i]=Bands[i]*.8f;maximum=Math.Max(maximum,bands[i]);}
        if(maximum<.001f&&Rms<.001f&&Peak<.001f)return Silent;
        return new(Rms*.8f,Bass*.8f,Mid*.8f,Treble*.8f,Peak*.8f,0,bands);
    }
    public static readonly AudioFrame Silent=new(0,0,0,0,0,0,new float[16]);
}

// Streaming Hann-windowed FFT. Input and analysis stay in memory.
public sealed class AudioSpectrum
{
    const int Size=2048;
    readonly float[] samples=new float[Size], levels=new float[16];
    readonly Complex[] fft=new Complex[Size];
    int position;
    float reference=.08f,previousRms,peak;
    public AudioFrame Latest {get;private set;}=AudioFrame.Silent;
    public void Add(float sample,int sampleRate,float sensitivity)
    {
        samples[position++]=float.IsFinite(sample)?Math.Clamp(sample,-1,1):0;
        if(position<Size)return;
        position=0;
        double sum=0;float maximum=0;
        for(int i=0;i<Size;i++)
        {
            float value=samples[i];sum+=value*value;maximum=Math.Max(maximum,Math.Abs(value));
            fft[i].X=(float)(value*.5*(1-Math.Cos(2*Math.PI*i/(Size-1))));fft[i].Y=0;
        }
        float rms=(float)Math.Sqrt(sum/Size);
        reference=Math.Max(.08f,Math.Max(rms,reference*.995f));
        float intensity=rms<.0007f?0:Math.Clamp(rms/reference*sensitivity,0,1);
        float onset=Math.Clamp((rms-previousRms)/reference*3*sensitivity,0,1);
        previousRms=rms;
        peak=Math.Max(maximum,peak*.85f);
        FastFourierTransform.FFT(true,11,fft);
        for(int band=0;band<16;band++)
        {
            double low=40*Math.Pow(400,band/16d),high=40*Math.Pow(400,(band+1)/16d);
            int first=Math.Max(1,(int)Math.Ceiling(low*Size/sampleRate));
            int last=Math.Min(Size/2-1,Math.Max(first,(int)Math.Ceiling(high*Size/sampleRate)-1));
            double energy=0;
            for(int bin=first;bin<=last;bin++)energy+=fft[bin].X*fft[bin].X+fft[bin].Y*fft[bin].Y;
            float target=rms<.0007f?0:Math.Clamp((float)Math.Sqrt(energy)*4/reference*sensitivity,0,1);
            levels[band]+=(target-levels[band])*(target>levels[band]?.7f:.18f);
            if(levels[band]<.001f)levels[band]=0;
        }
        float Average(int start,int end){float value=0;for(int i=start;i<end;i++)value+=levels[i];return value/(end-start);}
        Latest=new(intensity,Average(0,5),Average(5,11),Average(11,16),peak,onset,(float[])levels.Clone());
    }
}
