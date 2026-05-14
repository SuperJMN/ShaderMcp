using FluentAssertions;
using ShaderMcp.Core;

namespace ShaderMcp.Tests;

public sealed class SkiaShaderDocumentComposerTests
{
    [Fact]
    public void Compose_ShouldWrapFragmentMainWithNativeRuntimeEffectContract()
    {
        var manifest = new ShaderManifest(
            Name: "Tinted",
            Description: null,
            Parameters:
            [
                new ShaderParameter("tint", ShaderParameterKind.Color, [0.2f, 0.4f, 1, 1]),
                new ShaderParameter("strength", ShaderParameterKind.Number, [0.75f, 0, 0, 0])
            ]);

        var document = SkiaShaderDocumentComposer.Compose(SampleShaders.Valid, manifest);

        document.Should().Contain("uniform float2 resolution;");
        document.Should().Contain("uniform float time;");
        document.Should().Contain("uniform float frame;");
        document.Should().Contain("uniform float2 pointer;");
        document.Should().Contain("uniform shader inputTexture;");
        document.Should().Contain("uniform float4 tint;");
        document.Should().Contain("uniform float4 strength;");
        document.Should().Contain("half4 main(float2 fragCoord)");
        document.Should().Contain("return fragmentMain(fragCoord, uv);");
    }

    [Theory]
    [InlineData("bad-name")]
    [InlineData("1bad")]
    [InlineData("with space")]
    public void Compose_WhenParameterNameIsNotIdentifier_ShouldFail(string parameterName)
    {
        var manifest = new ShaderManifest(
            Name: "Invalid",
            Description: null,
            Parameters: [new ShaderParameter(parameterName, ShaderParameterKind.Number, [1, 0, 0, 0])]);

        var action = () => SkiaShaderDocumentComposer.Compose(SampleShaders.Valid, manifest);

        action.Should().Throw<ShaderValidationException>()
            .Where(x => x.ErrorCode == ShaderErrorCodes.InvalidParameterName);
    }
}
