using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NotchFlow.Configuration;
using NotchFlow.Models;
namespace NotchFlow.Services;
public sealed class PersonalStore
{
    public string Path {get;}
    public bool ReadOnly {get;private set;}
    public string Warning {get;private set;}="";
    public PersonalStore(string? directory=null)=>Path=System.IO.Path.Combine(directory??Log.DirectoryPath,"notes-tasks.json");
    PersonalDocument Read(string path)
    {
        var value=AtomicJson.Read(path,DataJsonContext.Default.PersonalDocument);
        if(value.Version!=1)throw new InvalidDataException("Неизвестная версия заметок. Файл сохранён без изменений.");
        if(value.Notes==null||value.Tasks==null||value.Notes.Any(x=>x==null)||value.Tasks.Any(x=>x==null))throw new JsonException("Некорректный список заметок/задач");
        if(value.Notes.Select(x=>x.Id).Distinct().Count()!=value.Notes.Count||value.Tasks.Select(x=>x.Id).Distinct().Count()!=value.Tasks.Count)throw new JsonException("Повторяющийся идентификатор");
        return value;
    }
    public PersonalDocument Load()
    {
        if(!File.Exists(Path))return new();
        try{return Read(Path);}
        catch(Exception ex)
        {
            Log.Error(ex);
            if(ex is JsonException && File.Exists(Path+".bak"))
            {
                try{var backup=Read(Path+".bak");File.Move(Path,Path+".corrupt-"+Guid.NewGuid().ToString("N"));AtomicJson.Write(Path,backup,DataJsonContext.Default.PersonalDocument);Warning="Восстановлена резервная копия. Повреждённый файл сохранён отдельно.";return backup;}
                catch(Exception backupError){Log.Error(backupError);}
            }
            ReadOnly=true;Warning="Не удалось открыть заметки. Файлы сохранены; запись заблокирована: "+ex.GetBaseException().Message;
            return new();
        }
    }
    public void Save(PersonalDocument value){if(ReadOnly)throw new IOException(Warning);AtomicJson.Write(Path,value,DataJsonContext.Default.PersonalDocument);}
}

