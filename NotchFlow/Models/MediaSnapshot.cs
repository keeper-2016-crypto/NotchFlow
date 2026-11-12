using System;
using System.Windows.Media;
namespace NotchFlow.Models;
public sealed record MediaSnapshot(string Title,string Artist,ImageSource? Artwork,bool Playing,bool CanPlay,bool CanPause,bool CanPrevious,bool CanNext,TimeSpan Position,TimeSpan Duration,DateTimeOffset Updated,double Rate,string SourceId="",string Identity="");

