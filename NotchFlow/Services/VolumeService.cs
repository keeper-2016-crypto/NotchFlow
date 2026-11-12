using System;
using System.Windows;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
namespace NotchFlow.Services;
public sealed class VolumeService : IMMNotificationClient, IDisposable
{
 MMDeviceEnumerator? enumerator; MMDevice? device; bool disposed;
 public event Action<int,bool>? Changed;
 public event Action<int,bool,bool>? StateChanged;
 public void SetLevel(int level) {try {if(device!=null){device.AudioEndpointVolume.MasterVolumeLevelScalar=Math.Clamp(level,0,100)/100f;device.AudioEndpointVolume.Mute=false;}}catch(Exception ex){Log.Error(ex);}}
 public void ToggleMute() {try {if(device!=null)device.AudioEndpointVolume.Mute=!device.AudioEndpointVolume.Mute;}catch(Exception ex){Log.Error(ex);}}
 public void Start() { try { enumerator=new MMDeviceEnumerator(); enumerator.RegisterEndpointNotificationCallback(this); Attach(); } catch(Exception ex) {Log.Error(ex);} }
 void Attach() { if(disposed)return; Detach(); StateChanged?.Invoke(0,false,false); try { device=enumerator?.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia); if(device!=null){device.AudioEndpointVolume.OnVolumeNotification+=VolumeChanged;StateChanged?.Invoke((int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar*100),device.AudioEndpointVolume.Mute,true);} } catch(Exception ex) {Log.Error(ex);} }
 void VolumeChanged(AudioVolumeNotificationData data) { var percent=(int)Math.Round(data.MasterVolume*100); var mute=data.Muted; Dispatch(()=>{if(!disposed){StateChanged?.Invoke(percent,mute,true);Changed?.Invoke(percent,mute);}}); }
 static void Dispatch(Action action) { if(!Application.Current.Dispatcher.HasShutdownStarted) Application.Current.Dispatcher.BeginInvoke(action); }
 void Detach() {if(device!=null) {device.AudioEndpointVolume.OnVolumeNotification-=VolumeChanged;device.Dispose();device=null;} }
 public void OnDefaultDeviceChanged(DataFlow flow,Role role,string deviceId) { if(flow==DataFlow.Render&&role==Role.Multimedia)Dispatch(Attach); }
 public void OnDeviceStateChanged(string deviceId,DeviceState newState) { }
 public void OnDeviceAdded(string deviceId) { }
 public void OnDeviceRemoved(string deviceId) { }
 public void OnPropertyValueChanged(string deviceId,PropertyKey key) { }
 public void Dispose() { disposed=true;Detach();if(enumerator!=null) {enumerator.UnregisterEndpointNotificationCallback(this);enumerator.Dispose();enumerator=null;} }
}

