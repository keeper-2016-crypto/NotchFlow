using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using NotchFlow.Helpers;
using NotchFlow.Models;
using NotchFlow.Services;
namespace NotchFlow.ViewModels;
public sealed class PersonalViewModel : ObservableObject,IDisposable
{
    readonly PersonalStore store;
    readonly DispatcherTimer debounce=new(){Interval=TimeSpan.FromMilliseconds(600)};
    bool dirty;string status="",taskDraft="";NoteItem? selectedNote;
    public ObservableCollection<NoteItem> Notes {get;}=new();
    public ObservableCollection<TaskItem> Tasks {get;}=new();
    public bool CanEdit=>!store.ReadOnly;
    public string Warning=>store.Warning;
    public bool AnimationsEnabled {get;set;}=true;
    public string Status {get=>status;private set{if(Set(ref status,value))Raise(nameof(DisplayError));}} public string DisplayError=>Status.StartsWith("Не сохранено:")?Status:Warning;
    public string TaskDraft {get=>taskDraft;set=>Set(ref taskDraft,value);}
    public NoteItem? SelectedNote {get=>selectedNote;set{if(Set(ref selectedNote,value)){Raise(nameof(EditingNote));Raise(nameof(NoteDates));}}}
    public bool EditingNote=>SelectedNote!=null;
    public string NoteDates=>SelectedNote==null?"":$"Создана {SelectedNote.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm} · Изменена {SelectedNote.UpdatedAt.LocalDateTime:dd.MM HH:mm}";
    public string TaskSummary=>$"Задачи — {Tasks.Count(x=>x.IsCompleted)}/{Tasks.Count} выполнено";
    public bool NoNotes=>Notes.Count==0;
    public bool NoTasks=>Tasks.Count==0;
    public PersonalViewModel(PersonalStore store)
    {
        this.store=store;var document=store.Load();
        foreach(var note in document.Notes){Notes.Add(note);note.PropertyChanged+=ItemChanged;}
        foreach(var task in document.Tasks){Tasks.Add(task);task.PropertyChanged+=ItemChanged;}
        Status=store.Warning.Length>0?store.Warning:"Все изменения сохраняются автоматически";
        debounce.Tick+=SaveTick;
    }
    void ItemChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(TaskItem.IsEditing))return;
        if(sender is NoteItem note)note.UpdatedAt=DateTimeOffset.UtcNow;
        if(sender is TaskItem task)task.UpdatedAt=DateTimeOffset.UtcNow;
        Raise(nameof(NoteDates));Raise(nameof(TaskSummary));MarkDirty();
    }
    void MarkDirty(){if(!CanEdit)return;dirty=true;Status="Сохранение…";debounce.Stop();debounce.Start();Raise(nameof(NoNotes));Raise(nameof(NoTasks));}
    void SaveTick(object? sender,EventArgs e)=>Flush();
    public void AddNote(){if(!CanEdit)return;var note=new NoteItem();note.PropertyChanged+=ItemChanged;Notes.Insert(0,note);SelectedNote=note;MarkDirty();}
    public void DeleteSelectedNote(){if(!CanEdit||SelectedNote==null)return;var note=SelectedNote;note.PropertyChanged-=ItemChanged;SelectedNote=null;Notes.Remove(note);MarkDirty();Flush();}
    public void AddTask(){if(!CanEdit||string.IsNullOrWhiteSpace(TaskDraft))return;var task=new TaskItem{Text=TaskDraft.Trim()};task.PropertyChanged+=ItemChanged;Tasks.Add(task);TaskDraft="";Raise(nameof(TaskSummary));MarkDirty();}
    public void RemoveTask(TaskItem task){if(!CanEdit)return;task.PropertyChanged-=ItemChanged;Tasks.Remove(task);Raise(nameof(TaskSummary));MarkDirty();Flush();}
    public bool Flush()
    {
        debounce.Stop();if(!dirty)return true;
        try{store.Save(new PersonalDocument{Notes=Notes.ToList(),Tasks=Tasks.ToList()});dirty=false;Status=$"Сохранено {DateTime.Now:HH:mm:ss}";return true;}
        catch(Exception ex){Log.Error(ex);Status="Не сохранено: "+ex.GetBaseException().Message;return false;}
    }
    public void Dispose(){Flush();debounce.Stop();debounce.Tick-=SaveTick;foreach(var n in Notes)n.PropertyChanged-=ItemChanged;foreach(var t in Tasks)t.PropertyChanged-=ItemChanged;}
}

