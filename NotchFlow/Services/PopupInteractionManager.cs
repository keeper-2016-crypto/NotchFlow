using System;
using System.Collections.Generic;

namespace NotchFlow.Services;

// One owner per active interaction; releasing one owner cannot release another.
public sealed class PopupInteractionManager
{
    readonly HashSet<object> owners = new();
    public bool IsActive => owners.Count > 0;
    public event Action? Changed;
    public void Set(object owner, bool active)
    {
        bool changed = active ? owners.Add(owner) : owners.Remove(owner);
        if (changed) Changed?.Invoke();
    }
    public void Clear() { owners.Clear(); Changed?.Invoke(); }
}
