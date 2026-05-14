using System.Text.Json.Serialization;

namespace ShaderMcp.Core;

[JsonConverter(typeof(JsonStringEnumConverter<ShaderParameterKind>))]
public enum ShaderParameterKind
{
    Number,
    Color,
    Vector,
}

public sealed record ShaderParameter(
    string Name,
    ShaderParameterKind Kind,
    IReadOnlyList<float> DefaultValue);

public sealed record ShaderVisualAssertions(
    bool RequireNonBlank = true,
    double MinColorVariance = 0.0001,
    int MinNonTransparentPixels = 1);

public sealed record ShaderTestCase(
    string Name,
    int Width = 512,
    int Height = 512,
    double Time = 0,
    int Frame = 0,
    float PointerX = 0,
    float PointerY = 0,
    IReadOnlyDictionary<string, IReadOnlyList<float>>? Parameters = null,
    ShaderVisualAssertions? Assertions = null,
    string? InputTexturePath = null,
    string? SnapshotPath = null,
    double SnapshotMaxDiff = 0.02);

public sealed record ShaderManifest(
    string Name,
    string? Description,
    IReadOnlyList<ShaderParameter> Parameters,
    IReadOnlyList<ShaderTestCase>? Tests = null)
{
    public static ShaderManifest Default { get; } = new("Untitled Shader", null, []);
}

public sealed record CreateShaderRequest(
    string Id,
    string Source,
    ShaderManifest Manifest,
    bool Overwrite = false);

public sealed record UpdateShaderRequest(
    string Id,
    string? Source = null,
    ShaderManifest? Manifest = null);

public sealed record ShaderDocument(string Id, string Source, ShaderManifest Manifest);

public sealed record ShaderSummary(string Id, string Name, string? Description);

public sealed record RenderRequest(
    ShaderDocument Shader,
    ShaderTestCase TestCase,
    string OutputPath);

public sealed record RenderSequenceRequest(
    ShaderDocument Shader,
    ShaderTestCase TestCase,
    string OutputDirectory,
    int FrameCount,
    double TimeStep);

public sealed record HarnessDiagnostic(string Severity, string Message, int? Line = null, int? Column = null);

public sealed record PixelStats(
    int Width,
    int Height,
    int NonTransparentPixels,
    int NonBlackPixels,
    double ColorVariance,
    double AverageAlpha);

public sealed record ShaderValidationResult(bool Ok, IReadOnlyList<HarnessDiagnostic> Diagnostics);

public sealed record ShaderRenderResult(
    bool Ok,
    string? OutputPath,
    PixelStats? Stats,
    IReadOnlyList<HarnessDiagnostic> Diagnostics,
    string? ErrorCode = null,
    string? Error = null);

public sealed record ShaderTestResult(
    string Name,
    bool Passed,
    string? OutputPath,
    PixelStats? Stats,
    string? ErrorCode,
    string? Error,
    IReadOnlyList<HarnessDiagnostic> Diagnostics);

public sealed record ShaderTestRunResult(bool Passed, IReadOnlyList<ShaderTestResult> Tests);

public sealed record ShaderFrameResult(
    int Index,
    double Time,
    string OutputPath,
    bool Passed,
    PixelStats? Stats,
    string? ErrorCode,
    string? Error,
    IReadOnlyList<HarnessDiagnostic> Diagnostics);

public sealed record ShaderSequenceResult(bool Passed, IReadOnlyList<ShaderFrameResult> Frames);
