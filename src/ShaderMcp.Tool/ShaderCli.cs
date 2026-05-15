using System.Text.Json;
using ShaderMcp.Core;

namespace ShaderMcp.Tool;

internal static class ShaderCli
{
    public static async Task<int> Run(string[] args)
    {
        if (args.Length == 0)
            return 0;

        if (args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        var runner = new NativeShaderRunner();

        try
        {
            return command switch
            {
                "validate" => await Validate(runner, options),
                "render" => await Render(runner, options),
                "sequence" => await Sequence(runner, options),
                _ => Error($"Unknown command '{command}'."),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error(ex.Message);
        }
    }

    private static async Task<int> Validate(NativeShaderRunner runner, Dictionary<string, string> options)
    {
        var shader = await LoadShader(options);
        var result = runner.Validate(shader);
        Console.WriteLine(JsonSerializer.Serialize(result, ShaderJson.Options));
        return result.IsSuccess && result.Value.Ok ? 0 : 1;
    }

    private static async Task<int> Render(NativeShaderRunner runner, Dictionary<string, string> options)
    {
        var shader = await LoadShader(options);
        var output = Required(options, "out");
        var testCase = CreateTestCase(options, "frame");
        var result = runner.Render(new RenderRequest(shader, testCase, output));
        Console.WriteLine(JsonSerializer.Serialize(result, ShaderJson.Options));
        return result.IsSuccess && result.Value.Ok ? 0 : 1;
    }

    private static async Task<int> Sequence(NativeShaderRunner runner, Dictionary<string, string> options)
    {
        var shader = await LoadShader(options);
        var outputDirectory = Required(options, "out-dir");
        var frameCount = ParseInt(options, "frames", 60);
        var timeStep = ParseDouble(options, "time-step", 1.0 / 60.0);
        var testCase = CreateTestCase(options, "sequence");
        var result = runner.RenderSequence(new RenderSequenceRequest(shader, testCase, outputDirectory, frameCount, timeStep));
        Console.WriteLine(JsonSerializer.Serialize(result, ShaderJson.Options));
        return result.IsSuccess && result.Value.Passed ? 0 : 1;
    }

    private static async Task<ShaderDocument> LoadShader(Dictionary<string, string> options)
    {
        if (options.TryGetValue("id", out var id))
        {
            var workspace = new ShaderWorkspace(options.GetValueOrDefault("workspace", ShaderWorkspaceRoot.ResolveDefault()));
            var shader = await workspace.Get(id);
            if (shader.IsFailure)
                throw new InvalidOperationException($"{shader.ErrorCode}: {shader.Error}");

            return shader.Value;
        }

        var sourcePath = Required(options, "source");
        var manifest = await LoadManifest(options.GetValueOrDefault("manifest"));
        var source = await File.ReadAllTextAsync(sourcePath);
        return new ShaderDocument(Path.GetFileNameWithoutExtension(sourcePath), source, manifest);
    }

    private static async Task<ShaderManifest> LoadManifest(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ShaderManifest.Default;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<ShaderManifest>(json, ShaderJson.Options)
            ?? ShaderManifest.Default;
    }

    private static ShaderTestCase CreateTestCase(Dictionary<string, string> options, string name)
        => new(
            Name: name,
            Width: ParseInt(options, "width", 512),
            Height: ParseInt(options, "height", 512),
            Time: ParseDouble(options, "time", 0),
            Frame: ParseInt(options, "frame", 0),
            PointerX: ParseFloat(options, "pointer-x", 0),
            PointerY: ParseFloat(options, "pointer-y", 0),
            InputTexturePath: options.GetValueOrDefault("input-texture"));

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var list = args.ToList();

        for (var i = 0; i < list.Count; i++)
        {
            var key = list[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
                continue;

            key = key[2..];
            if (i + 1 >= list.Count || list[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result[key] = "true";
                continue;
            }

            result[key] = list[++i];
        }

        return result;
    }

    private static string Required(Dictionary<string, string> options, string key)
        => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Missing required option --{key}.");

    private static int ParseInt(Dictionary<string, string> options, string key, int fallback)
        => options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static double ParseDouble(Dictionary<string, string> options, string key, double fallback)
        => options.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) ? parsed : fallback;

    private static float ParseFloat(Dictionary<string, string> options, string key, float fallback)
        => options.TryGetValue(key, out var value) && float.TryParse(value, out var parsed) ? parsed : fallback;

    private static int Error(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
        Console.Error.WriteLine(HelpText);
        return 2;
    }

    private const string HelpText = """
        shader-mcp CLI

        MCP server:
          shader-mcp

        Native runner commands:
          shader-mcp validate --source shader.sksl [--manifest shader.json]
          shader-mcp render --source shader.sksl --out frame.png [--width 512 --height 512 --time 0]
          shader-mcp sequence --source shader.sksl --out-dir frames --frames 60 [--time-step 0.0166667]

        Workspace shader:
          shader-mcp render --workspace /path/to/workspace --id shader-id --out frame.png

        Source contract:
          half4 fragmentMain(float2 fragCoord, float2 uv) { return half4(uv.x, uv.y, 0.5, 1.0); }

        Built-ins:
          resolution, time, frame, pointer, inputTexture
        """;
}
