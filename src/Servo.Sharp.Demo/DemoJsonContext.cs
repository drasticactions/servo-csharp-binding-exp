using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servo.Sharp.Demo;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(JsonDocument))]
internal partial class DemoJsonContext : JsonSerializerContext;
