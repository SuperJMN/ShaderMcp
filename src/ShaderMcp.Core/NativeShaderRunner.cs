using SkiaSharp;

namespace ShaderMcp.Core;

public sealed class NativeShaderRunner
{
    public ShaderResult<ShaderValidationResult> Validate(ShaderDocument shader)
    {
        using var compiled = Compile(shader);
        return compiled.IsFailure
            ? ShaderResult<ShaderValidationResult>.Success(new ShaderValidationResult(false, [compiled.Diagnostic]))
            : ShaderResult<ShaderValidationResult>.Success(new ShaderValidationResult(true, []));
    }

    public ShaderResult<ShaderRenderResult> Render(RenderRequest request)
    {
        using var compiled = Compile(request.Shader);
        if (compiled.IsFailure)
        {
            return ShaderResult<ShaderRenderResult>.Success(new ShaderRenderResult(
                Ok: false,
                OutputPath: null,
                Stats: null,
                Diagnostics: [compiled.Diagnostic],
                ErrorCode: ShaderErrorCodes.ValidationFailed,
                Error: compiled.Diagnostic.Message));
        }

        var effect = compiled.Effect!;
        var testCase = Normalize(request.TestCase);
        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);

        try
        {
            using var surface = SKSurface.Create(new SKImageInfo(testCase.Width, testCase.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (surface is null)
                return RenderFailure(ShaderErrorCodes.HarnessError, "Could not create native offscreen surface.");

            using var uniforms = CreateUniforms(effect, request.Shader.Manifest, testCase);
            using var inputTexture = CreateInputTexture(testCase);
            using var inputShader = inputTexture.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKSamplingOptions.Default);
            using var children = new SKRuntimeEffectChildren(effect);
            children["inputTexture"] = inputShader;

            using var shader = effect.ToShader(uniforms, children);
            if (shader is null)
                return RenderFailure(ShaderErrorCodes.HarnessError, "Could not create native shader instance.");

            using var paint = new SKPaint { Shader = shader, IsAntialias = false };
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawRect(new SKRect(0, 0, testCase.Width, testCase.Height), paint);
            surface.Canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using (var stream = File.Create(request.OutputPath))
                data.SaveTo(stream);

            using var bitmap = SKBitmap.FromImage(image);
            var stats = PixelStatsCalculator.Calculate(bitmap);
            var assertion = EvaluateAssertions(testCase, stats);

            return ShaderResult<ShaderRenderResult>.Success(new ShaderRenderResult(
                Ok: assertion is null,
                OutputPath: request.OutputPath,
                Stats: stats,
                Diagnostics: assertion is null ? [] : [assertion],
                ErrorCode: assertion is null ? null : ShaderErrorCodes.VisualAssertionFailed,
                Error: assertion?.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RenderFailure(ShaderErrorCodes.HarnessError, ex.Message);
        }
    }

    public ShaderResult<ShaderSequenceResult> RenderSequence(RenderSequenceRequest request)
    {
        if (request.FrameCount < 1)
            return ShaderResult<ShaderSequenceResult>.Failure(ShaderErrorCodes.InvalidRequest, "Frame count must be greater than zero.");

        Directory.CreateDirectory(request.OutputDirectory);
        var frames = new List<ShaderFrameResult>();

        for (var index = 0; index < request.FrameCount; index++)
        {
            var testCase = request.TestCase with
            {
                Name = $"{request.TestCase.Name}-{index:0000}",
                Time = request.TestCase.Time + index * request.TimeStep,
                Frame = request.TestCase.Frame + index,
            };

            var outputPath = Path.Combine(request.OutputDirectory, $"{SafeFileNames.Sanitize(testCase.Name)}.png");
            var render = Render(new RenderRequest(request.Shader, testCase, outputPath));
            if (render.IsFailure)
                return ShaderResult<ShaderSequenceResult>.Failure(render.ErrorCode, render.Error);

            frames.Add(new ShaderFrameResult(
                Index: index,
                Time: testCase.Time,
                OutputPath: outputPath,
                Passed: render.Value.Ok,
                Stats: render.Value.Stats,
                ErrorCode: render.Value.ErrorCode,
                Error: render.Value.Error,
                Diagnostics: render.Value.Diagnostics));
        }

        return ShaderResult<ShaderSequenceResult>.Success(new ShaderSequenceResult(
            Passed: frames.All(x => x.Passed),
            Frames: frames));
    }

    public ShaderResult<ShaderTestRunResult> RunTests(ShaderDocument shader, string artifactsDirectory)
    {
        Directory.CreateDirectory(artifactsDirectory);
        var testCases = shader.Manifest.Tests is { Count: > 0 }
            ? shader.Manifest.Tests
            : [new ShaderTestCase("default")];

        var results = new List<ShaderTestResult>();
        foreach (var testCase in testCases)
        {
            var outputPath = Path.Combine(artifactsDirectory, $"{SafeFileNames.Sanitize(testCase.Name)}.png");
            var render = Render(new RenderRequest(shader, testCase, outputPath));

            if (render.IsFailure)
            {
                results.Add(new ShaderTestResult(testCase.Name, false, null, null, render.ErrorCode, render.Error, []));
                continue;
            }

            results.Add(new ShaderTestResult(
                testCase.Name,
                render.Value.Ok,
                render.Value.OutputPath,
                render.Value.Stats,
                render.Value.ErrorCode,
                render.Value.Error,
                render.Value.Diagnostics));
        }

        return ShaderResult<ShaderTestRunResult>.Success(new ShaderTestRunResult(
            Passed: results.All(x => x.Passed),
            Tests: results));
    }

    private static CompileResult Compile(ShaderDocument shader)
    {
        try
        {
            var source = SkiaShaderDocumentComposer.Compose(shader.Source, shader.Manifest);
            var effect = SKRuntimeEffect.CreateShader(source, out var errors);
            if (effect is null)
                return CompileResult.Failure(ParseDiagnostic(errors));

            return CompileResult.Success(effect);
        }
        catch (ShaderValidationException ex)
        {
            return CompileResult.Failure(new HarnessDiagnostic("error", ex.Message));
        }
    }

    private static SKRuntimeEffectUniforms CreateUniforms(SKRuntimeEffect effect, ShaderManifest manifest, ShaderTestCase testCase)
    {
        var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["resolution"] = new SKSize(testCase.Width, testCase.Height),
            ["time"] = (float)testCase.Time,
            ["frame"] = (float)testCase.Frame,
            ["pointer"] = new SKPoint(testCase.PointerX, testCase.PointerY),
        };

        foreach (var parameter in manifest.Parameters)
        {
            var values = ResolveParameterValue(parameter, testCase);
            uniforms[parameter.Name] = values;
        }

        return uniforms;
    }

    private static float[] ResolveParameterValue(ShaderParameter parameter, ShaderTestCase testCase)
    {
        var values = testCase.Parameters is not null && testCase.Parameters.TryGetValue(parameter.Name, out var overrideValue)
            ? overrideValue
            : parameter.DefaultValue;

        return PadToFloat4(values);
    }

    private static float[] PadToFloat4(IReadOnlyList<float> values)
    {
        var result = new float[4];
        for (var i = 0; i < Math.Min(values.Count, result.Length); i++)
            result[i] = values[i];
        return result;
    }

    private static SKImage CreateInputTexture(ShaderTestCase testCase)
    {
        if (!string.IsNullOrWhiteSpace(testCase.InputTexturePath))
        {
            var image = SKImage.FromEncodedData(testCase.InputTexturePath);
            if (image is not null)
                return image;
        }

        var width = Math.Max(2, Math.Min(256, testCase.Width));
        var height = Math.Max(2, Math.Min(256, testCase.Height));
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var checker = ((x / 8) + (y / 8)) % 2 == 0;
                var r = checker ? (byte)230 : (byte)35;
                var g = (byte)Math.Clamp(x * 255 / Math.Max(1, width - 1), 0, 255);
                var b = (byte)Math.Clamp(y * 255 / Math.Max(1, height - 1), 0, 255);
                bitmap.SetPixel(x, y, new SKColor(r, g, b, 255));
            }
        }

        return SKImage.FromBitmap(bitmap);
    }

    private static ShaderTestCase Normalize(ShaderTestCase testCase)
        => testCase with
        {
            Width = Math.Max(1, testCase.Width),
            Height = Math.Max(1, testCase.Height),
            Assertions = testCase.Assertions ?? new ShaderVisualAssertions(),
        };

    private static HarnessDiagnostic? EvaluateAssertions(ShaderTestCase testCase, PixelStats stats)
    {
        var assertions = testCase.Assertions ?? new ShaderVisualAssertions();

        if (assertions.RequireNonBlank && stats.NonBlackPixels == 0)
            return new HarnessDiagnostic("error", "Rendered frame is blank.");

        if (stats.ColorVariance < assertions.MinColorVariance)
            return new HarnessDiagnostic("error", $"Rendered frame variance {stats.ColorVariance:0.######} is below required {assertions.MinColorVariance:0.######}.");

        if (stats.NonTransparentPixels < assertions.MinNonTransparentPixels)
            return new HarnessDiagnostic("error", $"Rendered frame has {stats.NonTransparentPixels} non-transparent pixels, below required {assertions.MinNonTransparentPixels}.");

        return null;
    }

    private static HarnessDiagnostic ParseDiagnostic(string? errors)
    {
        var message = string.IsNullOrWhiteSpace(errors)
            ? "Shader compilation failed."
            : errors.Trim();

        return new HarnessDiagnostic("error", message);
    }

    private static ShaderResult<ShaderRenderResult> RenderFailure(string errorCode, string error)
        => ShaderResult<ShaderRenderResult>.Success(new ShaderRenderResult(
            Ok: false,
            OutputPath: null,
            Stats: null,
            Diagnostics: [new HarnessDiagnostic("error", error)],
            ErrorCode: errorCode,
            Error: error));

    private sealed record CompileResult(SKRuntimeEffect? Effect, HarnessDiagnostic Diagnostic) : IDisposable
    {
        public bool IsFailure => Effect is null;

        public static CompileResult Success(SKRuntimeEffect effect) => new(effect, new HarnessDiagnostic("info", string.Empty));

        public static CompileResult Failure(HarnessDiagnostic diagnostic) => new(null, diagnostic);

        public void Dispose() => Effect?.Dispose();
    }
}
