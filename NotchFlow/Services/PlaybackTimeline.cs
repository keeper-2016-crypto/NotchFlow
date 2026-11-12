using System;
using NotchFlow.Models;
namespace NotchFlow.Services;

// State and projection are independent of whether any window is visible.
public sealed class PlaybackTimeline
{
    MediaSnapshot? last;
    double anchor;
    DateTimeOffset anchorTime;
    public double Duration => Math.Max(0, last?.Duration.TotalSeconds ?? 0);
    public double Position(DateTimeOffset now)
    {
        double delta = last?.Playing == true ? Math.Max(0, (now - anchorTime).TotalSeconds) * ValidRate(last.Rate) : 0;
        return Math.Clamp(anchor + delta, 0, Duration);
    }
    static double ValidRate(double rate) => double.IsFinite(rate) && rate >= 0 ? rate : 1;
    public void Accept(MediaSnapshot? value, DateTimeOffset receivedAt)
    {
        if (value == null) { last = null; anchor = 0; anchorTime = receivedAt; return; }
        bool sameAnchor = last != null && last.Identity == value.Identity && last.SourceId == value.SourceId
            && last.Updated == value.Updated && last.Position == value.Position && last.Duration == value.Duration;
        if (sameAnchor && (last!.Playing != value.Playing || last.Rate != value.Rate))
        {
            // Some sources emit PlaybackInfoChanged without refreshing their timeline.
            anchor = Position(receivedAt); anchorTime = receivedAt;
        }
        else if (!sameAnchor)
        {
            anchor = Math.Clamp(value.Position.TotalSeconds, 0, Math.Max(0, value.Duration.TotalSeconds));
            anchorTime = value.Updated > receivedAt || value.Updated == default ? receivedAt : value.Updated;
        }
        last = value;
    }
}

public sealed class CompactPlaybackState
{
    DateTimeOffset? pausedAt;
    bool hasSession, playing;
    public void Accept(bool exists, bool isPlaying, DateTimeOffset now)
    {
        if (!exists || isPlaying) pausedAt = null;
        else if (!hasSession || playing || pausedAt == null) pausedAt = now;
        hasSession = exists; playing = isPlaying;
    }
    public bool ShowClock(DateTimeOffset now) => !hasSession || (!playing && pausedAt.HasValue && now - pausedAt.Value >= TimeSpan.FromSeconds(40));
    public TimeSpan? Remaining(DateTimeOffset now) => hasSession && !playing && pausedAt.HasValue && !ShowClock(now)
        ? TimeSpan.FromSeconds(40) - (now - pausedAt.Value) : null;
}
