using System.Text.Json.Serialization;

namespace Servo.Sharp;

[JsonSerializable(typeof(List<string>))]
internal partial class ServoJsonContext : JsonSerializerContext;
