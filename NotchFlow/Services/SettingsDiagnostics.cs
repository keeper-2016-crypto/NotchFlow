using System;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using NotchFlow.Configuration;
namespace NotchFlow.Services;
public static class SettingsDiagnostics
{
    public static int Run(string mode, string directory)
    {
        Directory.CreateDirectory(directory);
        var report = Path.Combine(directory, "check-" + mode + ".txt");
        try
        {
            var store = new SettingsStore(directory);
            switch (mode)
            {
                case "one": store.Save(new AppSettings { OffsetX = 17 }); break;
                case "many":
                    var first = store.Load();
                    if (first.OffsetX != 17) throw new InvalidDataException("Previous process setting missing");
                    first.Opacity = .83; first.Scale = 1.25; first.CapsuleColor = "#202124"; first.Animations = false; first.OffsetY = 9;
                    store.Save(first); break;
                case "read":
                    var restored = store.Load();
                    if (restored.OffsetX != 17 || restored.Scale != 1.25 || restored.Opacity != .83 || restored.Animations || restored.CapsuleColor != "#202124" || restored.OffsetY != 9)
                        throw new InvalidDataException("Settings differ after restart");
                    break;
                case "corrupt": File.WriteAllText(store.FilePath, "{broken"); break;
                case "recover":
                    if (store.Load().Scale != 1 || Directory.GetFiles(directory, "settings.json.corrupt-*").Length == 0)
                        throw new InvalidDataException("Corruption recovery failed");
                    store.Save(new()); break;
                default: throw new ArgumentException("Unknown diagnostic mode");
            }
            File.WriteAllText(report, $"PASS {mode}\nProcess: {Environment.ProcessPath}\nConfig: {store.FilePath}\nJson: {typeof(JsonSerializer).Assembly.FullName}\nEncoder: {typeof(JavaScriptEncoder).Assembly.FullName}\n");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(report, ex.ToString()); return 1; }
    }
}
