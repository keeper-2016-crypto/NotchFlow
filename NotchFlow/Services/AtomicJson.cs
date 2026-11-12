using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
namespace NotchFlow.Services;
public static class AtomicJson
{
    public static void Write<T>(string path,T value,JsonTypeInfo<T> type)
    {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,type);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){stream.Write(bytes);stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,path+".bak",true);else File.Move(temporary,path);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static T Read<T>(string path,JsonTypeInfo<T> type)=>JsonSerializer.Deserialize(File.ReadAllText(path),type)??throw new JsonException("Пустой документ");
}
