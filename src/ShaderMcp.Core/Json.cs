using System.Text.Json;

namespace ShaderMcp.Core;

public static class ShaderJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
