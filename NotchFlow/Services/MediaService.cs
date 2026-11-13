using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using NotchFlow.Models;
namespace NotchFlow.Services;
public sealed class MediaService : IDisposable
{
    GlobalSystemMediaTransportControlsSessionManager? manager;
    GlobalSystemMediaTransportControlsSession? session;
    readonly Dictionary<GlobalSystemMediaTransportControlsSession,bool> observed=new();
    readonly Dictionary<GlobalSystemMediaTransportControlsSession,DateTimeOffset> activity=new();
    public GlobalSystemMediaTransportControlsSession? SelectedSource=>session;
    public IReadOnlyList<GlobalSystemMediaTransportControlsSession> Sources=>observed.Keys.ToList();
    public void SelectSource(GlobalSystemMediaTransportControlsSession value){if(observed.ContainsKey(value)){activity[value]=DateTimeOffset.UtcNow;Choose(value,"manual");}}
    void Choose(GlobalSystemMediaTransportControlsSession? next,string reason){
        if(next==session){PublishTimeline();return;}
        generation++;session=next;sourceEpoch++;trackEpoch=0;title=artist="";artwork=null;metadataReady=false;
        try{Directory.CreateDirectory(Log.DirectoryPath);var path=Path.Combine(Log.DirectoryPath,"media-selection.log");if(File.Exists(path)&&new FileInfo(path).Length>1048576)File.Move(path,path+".old",true);File.AppendAllText(path,$"{DateTimeOffset.UtcNow:O} switch={reason} id={sourceEpoch} source={next?.SourceAppUserModelId} activity={(next!=null&&activity.TryGetValue(next,out var time)?time.ToString("O"):"unknown")}\n");}catch{}
        PublishTimeline();RefreshMetadata();
    }
    bool disposed;
    int generation, sourceEpoch, trackEpoch;
    string title="", artist="";
    bool metadataReady;
    ImageSource? artwork;
    public event Action<MediaSnapshot?>? Changed;
    public event Action<string>? Failed;
    public async Task StartAsync()
    {
        try
        {
            var result=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if(disposed)return;
            manager=result; manager.CurrentSessionChanged+=CurrentChanged; manager.SessionsChanged+=SessionsChanged; Attach();
        }
        catch(Exception ex){Log.Error(ex);Failed?.Invoke("Медиасервис Windows недоступен");}
    }
    static void Dispatch(Action action){if(!Application.Current.Dispatcher.HasShutdownStarted)Application.Current.Dispatcher.BeginInvoke(action);}
    void CurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender,CurrentSessionChangedEventArgs args)=>Dispatch(Attach);
    void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender,SessionsChangedEventArgs args)=>Dispatch(Attach);
    void Attach()
    {
        if(disposed)return;
        try {
            var available=manager?.GetSessions().ToList()??new();
            foreach(var old in observed.Keys.Except(available).ToList()){old.MediaPropertiesChanged-=PropertiesChanged;old.PlaybackInfoChanged-=PlaybackChanged;old.TimelinePropertiesChanged-=TimelineChanged;observed.Remove(old);activity.Remove(old);}
            var started=new List<GlobalSystemMediaTransportControlsSession>();
            foreach(var item in available){
                bool playing=item.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if(!observed.TryGetValue(item,out bool wasPlaying)){item.MediaPropertiesChanged+=PropertiesChanged;item.PlaybackInfoChanged+=PlaybackChanged;item.TimelinePropertiesChanged+=TimelineChanged;}
                if(playing&&!wasPlaying){activity[item]=DateTimeOffset.UtcNow;started.Add(item);}
                observed[item]=playing;
            }
            var next=session;
            if(next==null||!observed.ContainsKey(next))next=available.OrderByDescending(x=>observed[x]).ThenByDescending(x=>activity.GetValueOrDefault(x)).FirstOrDefault();
            else if(!observed[next]&&started.Count>0)next=started.OrderByDescending(x=>activity.GetValueOrDefault(x)).First();
            Choose(next,"session/playback observation");
        }catch(Exception ex){Log.Error(ex);}
    }
    void PropertiesChanged(GlobalSystemMediaTransportControlsSession sender,MediaPropertiesChangedEventArgs args)=>Dispatch(()=>{if(sender==session)RefreshMetadata();});
    void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender,PlaybackInfoChangedEventArgs args)=>Dispatch(()=>{if(sender==session)PublishTimeline();});
    void TimelineChanged(GlobalSystemMediaTransportControlsSession sender,TimelinePropertiesChangedEventArgs args)=>Dispatch(()=>{if(sender==session)PublishTimeline();});
    void PublishTimeline()
    {
        if(disposed)return;
        if(session==null){Changed?.Invoke(null);return;}
        if(!metadataReady)return;
        try
        {
            var playback=session.GetPlaybackInfo();var timeline=session.GetTimelineProperties();var c=playback.Controls;
            Changed?.Invoke(new(title,artist,artwork,playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                c.IsPlayEnabled,c.IsPauseEnabled,c.IsPreviousEnabled,c.IsNextEnabled,timeline.Position-timeline.StartTime,
                timeline.EndTime-timeline.StartTime,timeline.LastUpdatedTime,playback.PlaybackRate??1,
                sourceEpoch.ToString(),trackEpoch.ToString()));
        }
        catch(Exception ex){Log.Error(ex);Failed?.Invoke("Не удалось прочитать медиасессию");}
    }
    async void RefreshMetadata()
    {
        int version=++generation;var current=session;if(current==null)return;
        try
        {
            var properties=await current.TryGetMediaPropertiesAsync();
            if(disposed||version!=generation||current!=session)return;
                        // Some players publish empty metadata between tracks. Keep the last complete presentation.
            if(string.IsNullOrWhiteSpace(properties.Title) && metadataReady)return;
            metadataReady=true;
            if(title!=properties.Title||artist!=properties.Artist)trackEpoch++;
            title=properties.Title;artist=properties.Artist;PublishTimeline();
            if(properties.Thumbnail==null){artwork=null;PublishTimeline();return;}
            if(properties.Thumbnail!=null)
            {
                try
                {
                    using var stream=await properties.Thumbnail.OpenReadAsync();
                    var art=new BitmapImage();art.BeginInit();art.CacheOption=BitmapCacheOption.OnLoad;art.DecodePixelWidth=192;art.StreamSource=stream.AsStreamForRead();art.EndInit();art.Freeze();
                    if(disposed||version!=generation||current!=session)return;artwork=art;PublishTimeline();
                }
                catch(Exception ex){Log.Error(ex);}
            }
        }
        catch(Exception ex){Log.Error(ex);if(version==generation&&!disposed)Failed?.Invoke("Не удалось прочитать название композиции");}
    }
    public async Task ControlAsync(string operation)
    {
        var current=session;if(current==null)return;
        try{bool ok=operation switch{"previous"=>await current.TrySkipPreviousAsync(),"next"=>await current.TrySkipNextAsync(),_=>await current.TryTogglePlayPauseAsync()};if(!ok)Failed?.Invoke("Приложение отклонило команду");}
        catch(Exception ex){Log.Error(ex);Failed?.Invoke("Команда медиаплеера недоступна");}
    }
    void Detach(){if(session==null)return;session.MediaPropertiesChanged-=PropertiesChanged;session.PlaybackInfoChanged-=PlaybackChanged;session.TimelinePropertiesChanged-=TimelineChanged;session=null;}
    public void Dispose(){disposed=true;generation++;foreach(var item in observed.Keys){item.MediaPropertiesChanged-=PropertiesChanged;item.PlaybackInfoChanged-=PlaybackChanged;item.TimelinePropertiesChanged-=TimelineChanged;}observed.Clear();activity.Clear();Detach();if(manager!=null){manager.CurrentSessionChanged-=CurrentChanged;manager.SessionsChanged-=SessionsChanged;}manager=null;}
}


