namespace ShaderMcp.Tests;

internal static class TestWorkspace
{
    public static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "shader-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

internal static class SampleShaders
{
    public const string Valid = """
        half4 fragmentMain(float2 fragCoord, float2 uv) {
            return half4(uv.x, uv.y, 0.6 + 0.4 * sin(time), 1.0);
        }
        """;

    public const string UsesInputTexture = """
        half4 fragmentMain(float2 fragCoord, float2 uv) {
            return inputTexture.eval(fragCoord);
        }
        """;
}
