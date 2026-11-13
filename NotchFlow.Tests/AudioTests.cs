using NotchFlow.Services;
internal static class AudioTests
{
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    static AudioFrame Tone(double frequency,float amplitude= .08f)
    {
        var processor=new AudioSpectrum();
        for(int i=0;i<48000;i++)processor.Add(amplitude*(float)Math.Sin(2*Math.PI*frequency*i/48000),48000,1);
        return processor.Latest;
    }
    public static void Run()
    {
        var positive=Tone(100,.08f);var negative=Tone(100,-.08f);
        Check(AudioFrame.Combine(positive,negative).Bass>.1,"Opposite-phase stereo channels retain bass");
        var decaying=positive;
        for(int i=0;i<60;i++)decaying=decaying.Decay();
        Check(ReferenceEquals(decaying,AudioFrame.Silent),"Missing buffers decay to stable silence");
        var bass=Tone(100);var middle=Tone(1000);var treble=Tone(8000);
        Check(bass.Bass>bass.Mid*3 && bass.Bass>bass.Treble*3,"100 Hz excites bass bins");
        Check(middle.Mid>middle.Bass*3 && middle.Mid>middle.Treble*3,"1 kHz excites mid bins");
        Check(treble.Treble>treble.Bass*3 && treble.Treble>treble.Mid*3,"8 kHz excites treble bins");
        Check(Tone(100,.002f).Rms<Tone(100,.08f).Rms,"Quiet signal has lower intensity");
        var spectrum=new AudioSpectrum();
        for(int i=0;i<2048;i++)spectrum.Add(.2f*(float)Math.Sin(2*Math.PI*100*i/48000),48000,1);
        Check(spectrum.Latest.Onset>.1,"Sound attack produces onset");
        for(int i=0;i<48000;i++)spectrum.Add(0,48000,1);
        Check(spectrum.Latest.Bands.Max()<.02 && spectrum.Latest.Rms==0 && spectrum.Latest.Onset==0,"Silence releases spectrum and onsets");
        spectrum.Add(float.NaN,48000,1);spectrum.Add(float.PositiveInfinity,48000,1);
        Check(float.IsFinite(spectrum.Latest.Rms),"Invalid samples cannot propagate NaN");
    }
}
