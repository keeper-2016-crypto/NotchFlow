using System.Text.Json.Serialization;
using NotchFlow.Models;
namespace NotchFlow.Configuration;
[JsonSourceGenerationOptions(WriteIndented=true,GenerationMode=JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PersonalDocument))]
[JsonSerializable(typeof(WeatherSnapshot))]
internal partial class DataJsonContext : JsonSerializerContext { }
