using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using NotchFlow.Configuration;

namespace NotchFlow.Services;

public static class Log
{
    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotchFlow");
    public static void Error(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {ex}\n");
        }
        catch { /* Logging must not replace the original exception. */ }
    }
}

public sealed class SettingsStore
{
    public string FilePath { get; }
    // An explicit directory is only used by isolated diagnostics/tests.
    public SettingsStore(string? directory = null) => FilePath = Path.Combine(directory ?? Log.DirectoryPath, "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJsonContext.Default.AppSettings)
                ?? throw new JsonException("Settings root must be an object.");
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            Log.Error(ex);
            File.Move(FilePath, FilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
            return new();
        }
        // Loader, dependency and permission failures are not mislabeled as corrupt JSON.
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        // Serialize before touching any existing file.
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings, SettingsJsonContext.Default.AppSettings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(json);
                file.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
            else File.Move(temporary, FilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void SetAutoStart(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("NotchFlow", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("NotchFlow", false);
    }
}
