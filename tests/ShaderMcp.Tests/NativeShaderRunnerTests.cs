using FluentAssertions;
using ShaderMcp.Core;

namespace ShaderMcp.Tests;

public sealed class NativeShaderRunnerTests
{
    [Fact]
    public void Validate_WhenShaderIsValid_ShouldCompile()
    {
        var runner = new NativeShaderRunner();

        var result = runner.Validate(new ShaderDocument("valid", SampleShaders.Valid, ShaderManifest.Default));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Ok.Should().BeTrue();
        result.Value.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenShaderIsInvalid_ShouldReturnStructuredDiagnostics()
    {
        var runner = new NativeShaderRunner();
        var shader = new ShaderDocument("invalid", "half4 fragmentMain(float2 fragCoord, float2 uv) { nope }", ShaderManifest.Default);

        var result = runner.Validate(shader);

        result.IsSuccess.Should().BeTrue();
        result.Value.Ok.Should().BeFalse();
        result.Value.Diagnostics.Should().NotBeEmpty();
        result.Value.Diagnostics[0].Severity.Should().Be("error");
    }

    [Fact]
    public void Render_ShouldExportPngAndPixelStats()
    {
        var root = TestWorkspace.Create();
        var runner = new NativeShaderRunner();
        var outputPath = Path.Combine(root, "frame.png");
        var shader = new ShaderDocument("valid", SampleShaders.Valid, ShaderManifest.Default);

        var result = runner.Render(new RenderRequest(
            shader,
            new ShaderTestCase("frame", Width: 64, Height: 32, Time: 0.25),
            outputPath));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Ok.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
        new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        result.Value.Stats.Should().NotBeNull();
        result.Value.Stats!.Width.Should().Be(64);
        result.Value.Stats.Height.Should().Be(32);
        result.Value.Stats.NonTransparentPixels.Should().BeGreaterThan(0);
        result.Value.Stats.ColorVariance.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Render_ShouldProvideDeterministicKnownInputTexture()
    {
        var root = TestWorkspace.Create();
        var runner = new NativeShaderRunner();
        var outputPath = Path.Combine(root, "texture.png");
        var shader = new ShaderDocument("texture", SampleShaders.UsesInputTexture, ShaderManifest.Default);

        var result = runner.Render(new RenderRequest(
            shader,
            new ShaderTestCase("texture", Width: 32, Height: 32),
            outputPath));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Ok.Should().BeTrue();
        result.Value.Stats!.ColorVariance.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RenderSequence_ShouldExportDeterministicFrames()
    {
        var root = TestWorkspace.Create();
        var runner = new NativeShaderRunner();
        var shader = new ShaderDocument("sequence", SampleShaders.Valid, ShaderManifest.Default);

        var result = runner.RenderSequence(new RenderSequenceRequest(
            shader,
            new ShaderTestCase("seq", Width: 16, Height: 16),
            root,
            FrameCount: 3,
            TimeStep: 0.5));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Frames.Should().HaveCount(3);
        result.Value.Frames.Select(x => x.OutputPath).Should().OnlyContain(path => File.Exists(path));
    }

    [Fact]
    public void RenderSequence_ShouldSanitizeFrameNames()
    {
        var root = TestWorkspace.Create();
        var runner = new NativeShaderRunner();
        var shader = new ShaderDocument("sequence", SampleShaders.Valid, ShaderManifest.Default);

        var result = runner.RenderSequence(new RenderSequenceRequest(
            shader,
            new ShaderTestCase("../escape", Width: 8, Height: 8),
            root,
            FrameCount: 1,
            TimeStep: 0.5));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Frames[0].OutputPath.Should().StartWith(root);
        Path.GetFileName(result.Value.Frames[0].OutputPath).Should().NotContain("..");
    }
}
