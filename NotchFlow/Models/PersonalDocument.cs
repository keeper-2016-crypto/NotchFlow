using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NotchFlow.Helpers;
namespace NotchFlow.Models;
public sealed class NoteItem : ObservableObject
{
    public Guid Id {get;set;}=Guid.NewGuid();
    string title="Новая заметка",text="";
    public string Title {get=>title;set=>Set(ref title,value??"");}
    public string Text {get=>text;set=>Set(ref text,value??"");}
    public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt {get;set;}=DateTimeOffset.UtcNow;
}
public sealed class TaskItem : ObservableObject
{
    public Guid Id {get;set;}=Guid.NewGuid();
    string text="";bool completed,editing;
    public string Text {get=>text;set=>Set(ref text,value??"");}
    public bool IsCompleted {get=>completed;set=>Set(ref completed,value);}
    [JsonIgnore] public bool IsEditing {get=>editing;set=>Set(ref editing,value);}
    public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt {get;set;}=DateTimeOffset.UtcNow;
}
public sealed class PersonalDocument
{
    public int Version {get;set;}=1;
    public List<NoteItem> Notes {get;set;}=new();
    public List<TaskItem> Tasks {get;set;}=new();
}
