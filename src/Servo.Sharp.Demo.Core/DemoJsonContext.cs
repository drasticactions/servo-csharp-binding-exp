using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servo.Sharp.Demo.Core;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(JsonDocument))]
public partial class DemoJsonContext : JsonSerializerContext;
