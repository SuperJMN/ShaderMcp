using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ShaderMcp.Core;

namespace ShaderMcp.Tool.Tools;

[McpServerToolType]
public sealed class ShaderTools
{
    [McpServerTool(Name = "list_shaders"), Description("List shaders stored in the local shader workspace.")]
    public static async Task<string> ListShaders(ShaderWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var result = await workspace.List(cancellationToken);
        return Serialize(result);
    }

    [McpServerTool(Name = "get_shader"), Description("Get a shader's SkSL source and manifest by id.")]
    public static async Task<string> GetShader(
        ShaderWorkspace workspace,
        [Description("Shader id. Must match ^[a-z0-9][a-z0-9_-]{0,63}$.")] string id,
        CancellationToken cancellationToken = default)
    {
        var result = await workspace.Get(id, cancellationToken);
        return Serialize(result);
    }

    [McpServerTool(Name = "create_shader"), Description("Create a shader from native SkSL fragmentMain source and optional manifest JSON.")]
    public static async Task<string> CreateShader(
        ShaderWorkspace workspace,
        [Description("Shader id. Must match ^[a-z0-9][a-z0-9_-]{0,63}$.")] string id,
        [Description("SkSL source defining fragmentMain(float2 fragCoord, float2 uv) -> half4.")] string sksl,
        [Description("Optional ShaderManifest JSON. Omit for a default manifest.")] string? manifestJson = null,
        [Description("Replace an existing shader with the same id.")] bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var manifest = ParseManifest(manifestJson);
        if (manifest.IsFailure)
            return Serialize(manifest);

        var result = await workspace.Create(new CreateShaderRequest(id, sksl, manifest.Value, overwrite), cancellationToken);
        return Serialize(result);
    }

    [McpServerTool(Name = "update_shader"), Description("Update an existing shader's SkSL source and/or full manifest JSON.")]
    public static async Task<string> UpdateShader(
        ShaderWorkspace workspace,
        [Description("Shader id.")] string id,
        [Description("Replacement SkSL source. Omit to keep current source.")] string? sksl = null,
        [Description("Replacement ShaderManifest JSON. Omit to keep current manifest.")] string? manifestJson = null,
        CancellationToken cancellationToken = default)
    {
        var manifest = string.IsNullOrWhiteSpace(manifestJson)
            ? ShaderResult<ShaderManifest?>.Success(null)
            : ParseManifest(manifestJson).Map(x => (ShaderManifest?)x);

        if (manifest.IsFailure)
            return Serialize(manifest);

        var result = await workspace.Update(new UpdateShaderRequest(id, sksl, manifest.Value), cancellationToken);
        return Serialize(result);
    }

    [McpServerTool(Name = "validate_shader"), Description("Compile a saved or inline shader through the native ShaderRunner.")]
    public static async Task<string> ValidateShader(
        ShaderWorkspace workspace,
        NativeShaderRunner runner,
        [Description("Saved shader id. Required unless sksl is provided.")] string? id = null,
        [Description("Inline SkSL source. Required unless id is provided.")] string? sksl = null,
        [Description("Manifest JSON for inline SkSL. Omit for default manifest.")] string? manifestJson = null,
        CancellationToken cancellationToken = default)
    {
        var shader = await ResolveShader(workspace, id, sksl, manifestJson, cancellationToken);
        if (shader.IsFailure)
            return Serialize(shader);

        var result = runner.Validate(shader.Value);
        return Serialize(result);
    }

    [McpServerTool(Name = "render_shader"), Description("Render a saved shader offscreen to PNG and return image path plus pixel statistics.")]
    public static async Task<string> RenderShader(
        ShaderWorkspace workspace,
        NativeShaderRunner runner,
        [Description("Saved shader id.")] string id,
        [Description("Optional ShaderTestCase JSON.")] string? testCaseJson = null,
        [Description("Render width when testCaseJson is omitted.")] int width = 512,
        [Description("Render height when testCaseJson is omitted.")] int height = 512,
        [Description("Render time uniform when testCaseJson is omitted.")] double time = 0,
        CancellationToken cancellationToken = default)
    {
        var shader = await workspace.Get(id, cancellationToken);
        if (shader.IsFailure)
            return Serialize(shader);

        var testCase = ParseTestCase(testCaseJson, width, height, time);
        if (testCase.IsFailure)
            return Serialize(testCase);

        var outputPath = workspace.CreateArtifactPath(id, $"{testCase.Value.Name}.png");
        var result = runner.Render(new RenderRequest(shader.Value, testCase.Value, outputPath));
        return Serialize(result);
    }

    [McpServerTool(Name = "render_shader_sequence"), Description("Render a deterministic offscreen PNG frame sequence for a saved shader.")]
    public static async Task<string> RenderShaderSequence(
        ShaderWorkspace workspace,
        NativeShaderRunner runner,
        [Description("Saved shader id.")] string id,
        [Description("Number of frames to render.")] int frameCount,
        [Description("Seconds added to the time uniform between frames.")] double timeStep = 1.0 / 60.0,
        [Description("Optional base ShaderTestCase JSON.")] string? testCaseJson = null,
        [Description("Render width when testCaseJson is omitted.")] int width = 512,
        [Description("Render height when testCaseJson is omitted.")] int height = 512,
        CancellationToken cancellationToken = default)
    {
        var shader = await workspace.Get(id, cancellationToken);
        if (shader.IsFailure)
            return Serialize(shader);

        var testCase = ParseTestCase(testCaseJson, width, height, 0);
        if (testCase.IsFailure)
            return Serialize(testCase);

        var outputDirectory = Path.Combine(workspace.ArtifactsPath, id, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-sequence");
        var result = runner.RenderSequence(new RenderSequenceRequest(shader.Value, testCase.Value, outputDirectory, frameCount, timeStep));
        return Serialize(result);
    }

    [McpServerTool(Name = "run_shader_tests"), Description("Run manifest-defined shader visual tests, or a default nonblank smoke test if none are defined.")]
    public static async Task<string> RunShaderTests(
        ShaderWorkspace workspace,
        NativeShaderRunner runner,
        [Description("Saved shader id.")] string id,
        CancellationToken cancellationToken = default)
    {
        var shader = await workspace.Get(id, cancellationToken);
        if (shader.IsFailure)
            return Serialize(shader);

        var artifactsDirectory = Path.Combine(workspace.ArtifactsPath, id, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-tests");
        var result = runner.RunTests(shader.Value, artifactsDirectory);
        return Serialize(result);
    }

    private static async Task<ShaderResult<ShaderDocument>> ResolveShader(
        ShaderWorkspace workspace,
        string? id,
        string? sksl,
        string? manifestJson,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return await workspace.Get(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(sksl))
            return ShaderResult<ShaderDocument>.Failure(ShaderErrorCodes.InvalidRequest, "Either id or sksl is required.");

        var manifest = ParseManifest(manifestJson);
        return manifest.IsFailure
            ? ShaderResult<ShaderDocument>.Failure(manifest.ErrorCode, manifest.Error)
            : ShaderResult<ShaderDocument>.Success(new ShaderDocument("inline", sksl, manifest.Value));
    }

    private static ShaderResult<ShaderManifest> ParseManifest(string? manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            return ShaderResult<ShaderManifest>.Success(ShaderManifest.Default);

        try
        {
            var manifest = JsonSerializer.Deserialize<ShaderManifest>(manifestJson, ShaderJson.Options);
            return manifest is null
                ? ShaderResult<ShaderManifest>.Failure(ShaderErrorCodes.InvalidManifest, "Manifest JSON was empty.")
                : ShaderResult<ShaderManifest>.Success(manifest);
        }
        catch (JsonException ex)
        {
            return ShaderResult<ShaderManifest>.Failure(ShaderErrorCodes.InvalidManifest, ex.Message);
        }
    }

    private static ShaderResult<ShaderTestCase> ParseTestCase(string? testCaseJson, int width, int height, double time)
    {
        if (string.IsNullOrWhiteSpace(testCaseJson))
            return ShaderResult<ShaderTestCase>.Success(new ShaderTestCase("manual", Math.Max(1, width), Math.Max(1, height), time));

        try
        {
            var testCase = JsonSerializer.Deserialize<ShaderTestCase>(testCaseJson, ShaderJson.Options);
            return testCase is null
                ? ShaderResult<ShaderTestCase>.Failure(ShaderErrorCodes.InvalidRequest, "Test case JSON was empty.")
                : ShaderResult<ShaderTestCase>.Success(testCase);
        }
        catch (JsonException ex)
        {
            return ShaderResult<ShaderTestCase>.Failure(ShaderErrorCodes.InvalidRequest, ex.Message);
        }
    }

    private static string Serialize(ShaderResult result)
    {
        if (result.IsSuccess)
            return JsonSerializer.Serialize(new { ok = true }, ShaderJson.Options);

        return JsonSerializer.Serialize(new { ok = false, error = new { code = result.ErrorCode, message = result.Error } }, ShaderJson.Options);
    }

    private static string Serialize<T>(ShaderResult<T> result)
    {
        if (result.IsSuccess)
            return JsonSerializer.Serialize(new { ok = true, value = result.Value }, ShaderJson.Options);

        return JsonSerializer.Serialize(new { ok = false, error = new { code = result.ErrorCode, message = result.Error } }, ShaderJson.Options);
    }
}

file static class ShaderResultExtensions
{
    public static ShaderResult<TOut> Map<TIn, TOut>(this ShaderResult<TIn> result, Func<TIn, TOut> map)
        => result.IsSuccess
            ? ShaderResult<TOut>.Success(map(result.Value))
            : ShaderResult<TOut>.Failure(result.ErrorCode, result.Error);
}
