using System;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NotchFlow.Helpers;
using NotchFlow.Models;
using NotchFlow.Services;
namespace NotchFlow.ViewModels;
public sealed class IslandViewModel : ObservableObject,IDisposable
{
    readonly MediaService media;
    readonly Func<DateTimeOffset> now;
    readonly PlaybackTimeline timeline=new();
    readonly CompactPlaybackState compact=new();
    readonly DispatcherTimer ticker=new(){Interval=TimeSpan.FromSeconds(1)};
    readonly DispatcherTimer pauseTimer=new();
    readonly DispatcherTimer clockTimer=new();
    MediaSnapshot? snapshot;bool enabled=true,useClock;string selectedTab="Music";
    public string Title=>!enabled?"Медиаплеер отключён":snapshot?.Title is {Length:>0} t?t:"Ничего не воспроизводится";
    public string Artist=>!enabled?"Включите его в настройках":snapshot?.Artist??"Запустите музыку или видео";
    public ImageSource? Artwork=>enabled?snapshot?.Artwork:null;
    public bool Playing=>enabled&&snapshot?.Playing==true;
    public string PlayGlyph=>Playing?"❚❚":"▶";
    public bool CanToggle=>enabled&&snapshot!=null&&(snapshot.Playing?snapshot.CanPause:snapshot.CanPlay);
    public bool CanPrevious=>enabled&&snapshot?.CanPrevious==true;
    public bool CanNext=>enabled&&snapshot?.CanNext==true;
    double Position=>enabled?timeline.Position(now()):0;
    double End=>enabled?timeline.Duration:0;
    public string Elapsed=>Format(Position);
    public string Duration=>Format(End);
    public string Remaining=>"-"+Format(Math.Max(0,End-Position));
    public double Progress=>End>0?Position/End*100:0;
    public string ClockTime=>now().ToLocalTime().ToString("HH:mm");
    public string ClockDate=>now().ToLocalTime().ToString("d MMM",CultureInfo.GetCultureInfo("ru-RU"));
    public bool UseClock{get=>useClock;private set=>Set(ref useClock,value);}
    public string SelectedTab {get=>selectedTab;set{if(value is not ("Music" or "Weather" or "Notes" or "Tasks"))return;if(Set(ref selectedTab,value)&&value=="Weather")Weather.EnsureStarted();}}
    public PersonalViewModel Personal {get;}
    public WeatherViewModel Weather {get;}
    public Windows.Media.Control.GlobalSystemMediaTransportControlsSession? SelectedSource=>media.SelectedSource; public System.Collections.Generic.IReadOnlyList<Windows.Media.Control.GlobalSystemMediaTransportControlsSession> MediaSources=>media.Sources; public void SelectSource(Windows.Media.Control.GlobalSystemMediaTransportControlsSession source)=>media.SelectSource(source); public ICommand Toggle{get;}public ICommand Previous{get;}public ICommand Next{get;}
    public IslandViewModel(MediaService media,Func<DateTimeOffset>? now=null,string? dataDirectory=null)
    {
        this.media=media;this.now=now??(()=>DateTimeOffset.UtcNow);
        Personal=new(new PersonalStore(dataDirectory));Weather=new(new WeatherService(dataDirectory));
        Toggle=new RelayCommand(async()=>await media.ControlAsync("toggle"));Previous=new RelayCommand(async()=>await media.ControlAsync("previous"));Next=new RelayCommand(async()=>await media.ControlAsync("next"));
        media.Changed+=AcceptSnapshot;ticker.Tick+=Tick;pauseTimer.Tick+=PauseTick;clockTimer.Tick+=ClockTick;UpdateCompact();ClockTick(null,EventArgs.Empty);
    }
    public void AcceptSnapshot(MediaSnapshot? value)
    {
        snapshot=value;timeline.Accept(value,now());
        foreach(var property in new[]{nameof(Title),nameof(Artist),nameof(Artwork),nameof(Playing),nameof(PlayGlyph),nameof(CanToggle),nameof(CanPrevious),nameof(CanNext)})Raise(property);
        RefreshProgress();if(Playing)ticker.Start();else ticker.Stop();UpdateCompact();
    }
    public void SetEnabled(bool value){if(enabled==value)return;enabled=value;AcceptSnapshot(snapshot);}
    // A synchronous invalidation before the opening animation; the getters never cache an old position.
    public void SetVisible(bool value){if(value)RefreshProgress();}
    void Tick(object? sender,EventArgs e)=>RefreshProgress();
    void RefreshProgress(){Raise(nameof(Elapsed));Raise(nameof(Duration));Raise(nameof(Remaining));Raise(nameof(Progress));}
    void UpdateCompact()
    {
        compact.Accept(enabled&&snapshot!=null,Playing,now());UseClock=compact.ShowClock(now());pauseTimer.Stop();
        if(compact.Remaining(now()) is TimeSpan remaining){pauseTimer.Interval=remaining<TimeSpan.FromMilliseconds(1)?TimeSpan.FromMilliseconds(1):remaining;pauseTimer.Start();}
    }
    void PauseTick(object? sender,EventArgs e){pauseTimer.Stop();UpdateCompact();}
    void ClockTick(object? sender,EventArgs e){Raise(nameof(ClockTime));Raise(nameof(ClockDate));clockTimer.Stop();clockTimer.Interval=TimeSpan.FromSeconds(60-now().Second);clockTimer.Start();}
    static string Format(double seconds){var t=TimeSpan.FromSeconds(Math.Max(0,seconds));return t.TotalHours>=1?t.ToString(@"h\:mm\:ss"):t.ToString(@"m\:ss");}
    public void Dispose(){ticker.Stop();pauseTimer.Stop();clockTimer.Stop();ticker.Tick-=Tick;pauseTimer.Tick-=PauseTick;clockTimer.Tick-=ClockTick;media.Changed-=AcceptSnapshot;Weather.Dispose();Personal.Dispose();}
}


