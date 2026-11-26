using System;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NotchFlow.Services;

// Endpoint loopback is explicitly system-wide; GSMTC does not expose a reliable process ID.
public sealed class AudioAnalysisService : IDisposable
{
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(33)};
    readonly object gate=new();
    WasapiLoopbackCapture? capture;
    MMDevice? device;
    MMDeviceEnumerator? enumerator;
    AudioSpectrum leftSpectrum=new(),rightSpectrum=new();
    AudioFrame latest=AudioFrame.Silent;
    DateTime lastBuffer=DateTime.MinValue,lastDeviceCheck=DateTime.MinValue;
    float sensitivity=1;
    bool enabled,disposed,detailed;
    AudioFrame delivered=AudioFrame.Silent;
    public string Status {get;private set;}="Audio analysis off";
    public event Action<AudioFrame>? Changed;
    public AudioAnalysisService(){timer.Tick+=Tick;}
    public void Configure(bool value,double gain,bool spectrumFloor=false)
    {
        lock(gate){sensitivity=(float)gain;detailed=spectrumFloor;}
        if(disposed||enabled==value)return;
        enabled=value;
        if(value){lastDeviceCheck=DateTime.MinValue;timer.Start();Tick(null,EventArgs.Empty);}
        else {timer.Stop();StopCapture();enumerator?.Dispose();enumerator=null;Status="Audio analysis off";delivered=AudioFrame.Silent;Changed?.Invoke(delivered);}
    }
    void Tick(object? sender,EventArgs args)
    {
        if(!enabled||disposed)return;
        if((DateTime.UtcNow-lastDeviceCheck).TotalSeconds>=2)
        {
            lastDeviceCheck=DateTime.UtcNow;
            try
            {
                enumerator??=new MMDeviceEnumerator();
                var next=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
                if(capture!=null&&device?.ID==next.ID)next.Dispose();
                else
                {
                    StopCapture();device=next;

                    capture=new WasapiLoopbackCapture(device);
                    // Request an explicit float format supported by NAudio's shared-mode conversion.
                    capture.WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(48000,2);
                    capture.DataAvailable+=DataAvailable;capture.RecordingStopped+=Stopped;
                    capture.StartRecording();Status="System audio — all applications";
                }
            }
            catch(Exception ex){StopCapture();Status="Audio capture unavailable";Log.Error(ex);}
        }
        AudioFrame frame;
        lock(gate)frame=(DateTime.UtcNow-lastBuffer).TotalMilliseconds<200?latest:delivered.Decay();
        if(!ReferenceEquals(delivered,frame)){delivered=frame;Changed?.Invoke(frame);}
    }
    void DataAvailable(object? sender,WaveInEventArgs e)
    {
        lock(gate)
        {
            if(sender!=capture)return;
            var previous=leftSpectrum.Latest;
            for(int offset=0;offset+8<=e.BytesRecorded;offset+=8)
            {
                leftSpectrum.Add(BitConverter.ToSingle(e.Buffer,offset),48000,sensitivity,detailed);
                rightSpectrum.Add(BitConverter.ToSingle(e.Buffer,offset+4),48000,sensitivity,detailed);
            }
            if(!ReferenceEquals(previous,leftSpectrum.Latest))latest=AudioFrame.Combine(leftSpectrum.Latest,rightSpectrum.Latest);
            lastBuffer=DateTime.UtcNow;
        }
    }
    void Stopped(object? sender,StoppedEventArgs e)
    {
        timer.Dispatcher.BeginInvoke(new Action(()=>{
            if(sender!=capture||disposed)return;
            StopCapture();Status="Audio capture unavailable";
            if(e.Exception!=null)Log.Error(e.Exception);
        }));
    }
    void StopCapture()
    {
        WasapiLoopbackCapture? old;
        lock(gate){old=capture;capture=null;lastBuffer=DateTime.MinValue;leftSpectrum=new AudioSpectrum();rightSpectrum=new AudioSpectrum();latest=AudioFrame.Silent;}
        if(old!=null){old.DataAvailable-=DataAvailable;old.RecordingStopped-=Stopped;old.Dispose();}
        device?.Dispose();device=null;
    }
    public void Dispose(){disposed=true;timer.Stop();timer.Tick-=Tick;StopCapture();enumerator?.Dispose();}
}
