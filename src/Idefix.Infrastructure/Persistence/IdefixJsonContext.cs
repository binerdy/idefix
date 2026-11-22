using System.Text.Json.Serialization;
using Idefix.Core.Chat;
using Idefix.Core.Settings;
using Idefix.Infrastructure.Llm;

namespace Idefix.Infrastructure.Persistence;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(IdefixSettings))]
[JsonSerializable(typeof(ChatSession))]
[JsonSerializable(typeof(List<Project>))]
[JsonSerializable(typeof(ModelList))]
internal sealed partial class IdefixJsonContext : JsonSerializerContext;
