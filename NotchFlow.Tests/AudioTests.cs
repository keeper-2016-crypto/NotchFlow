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
    public static void FloorTests(){
        var effects=new NotchFlow.Controls.MediaEffects{Width=360,Height=150,Surface=new NotchFlow.Controls.IslandSurface()};
        var window=new System.Windows.Window{Content=effects,SizeToContent=System.Windows.SizeToContent.WidthAndHeight,ShowActivated=false,ShowInTaskbar=false,WindowStyle=System.Windows.WindowStyle.None};
        window.Show();
        try{
            var settings=new NotchFlow.Configuration.AppSettings{MediaVisualizer="Spectrum Floor",AudioAnalysisEnabled=true};
            effects.Configure(settings,null,true,true,true);
            effects.AcceptAudio(Tone(1000));
            Check(!effects.IsHitTestVisible,"Floor leaves pointer input to player");
            effects.Configure(settings,null,true,false,true);
            Check(!effects.IsAnimating,"Floor is inactive outside music tab");
            effects.Configure(settings,null,true,true,false);
            Check(!effects.IsAnimating,"Paused floor stops following incoming audio");
        }finally{window.Close();}
        int Peak(double hz){var data=Tone(hz).Floor;return Array.IndexOf(data,data.Max());}
        Check(Peak(100)<Peak(1000)&&Peak(1000)<Peak(8000),"Floor FFT orders bass, midrange and treble");
        Check(Tone(1000,.012f).Floor.Max()<Tone(1000,.08f).Floor.Max(),"Floor amplitude follows input gain");
        Check(Tone(1000,0).Floor.All(x=>x==0),"Floor has no silent baseline");
        var directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString());
        try{
            var store=new SettingsStore(directory);store.Save(new(){MediaVisualizer="Spectrum Floor",FloorCompact=true,FloorBars=48,FloorWidth=1,FloorColor="Custom",FloorHex="#BB4433"});
            var s=store.Load();Check(s.MediaVisualizer=="Spectrum Floor"&&s.FloorCompact&&s.FloorBars==48&&s.FloorWidth==20&&s.FloorHex=="#BB4433","Floor settings persist and clamp");
        }finally{if(System.IO.Directory.Exists(directory))System.IO.Directory.Delete(directory,true);}
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
