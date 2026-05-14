using FluentAssertions;
using ShaderMcp.Core;

namespace ShaderMcp.Tests;

public sealed class ShaderWorkspaceTests
{
    [Fact]
    public async Task Create_WhenShaderIsValid_ShouldPersistManifestAndSource()
    {
        var root = TestWorkspace.Create();
        var workspace = new ShaderWorkspace(root);
        var manifest = new ShaderManifest(
            Name: "Warm Plasma",
            Description: "A test shader",
            Parameters:
            [
                new ShaderParameter("tint", ShaderParameterKind.Color, [1, 0.2f, 0.1f, 1])
            ]);

        var result = await workspace.Create(new CreateShaderRequest("warm-plasma", SampleShaders.Valid, manifest));

        result.IsSuccess.Should().BeTrue(result.Error);
        var saved = await workspace.Get("warm-plasma");
        saved.Value.Manifest.Should().BeEquivalentTo(manifest);
        saved.Value.Source.Should().Be(SampleShaders.Valid);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("bad/name")]
    [InlineData("bad name")]
    [InlineData("")]
    public async Task Create_WhenIdIsUnsafe_ShouldFailWithoutWritingOutsideWorkspace(string id)
    {
        var root = TestWorkspace.Create();
        var workspace = new ShaderWorkspace(root);

        var result = await workspace.Create(new CreateShaderRequest(id, SampleShaders.Valid, ShaderManifest.Default));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ShaderErrorCodes.InvalidShaderId);
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenShaderAlreadyExistsAndOverwriteIsFalse_ShouldFail()
    {
        var workspace = new ShaderWorkspace(TestWorkspace.Create());
        await workspace.Create(new CreateShaderRequest("plasma", SampleShaders.Valid, ShaderManifest.Default));

        var result = await workspace.Create(new CreateShaderRequest("plasma", SampleShaders.Valid, ShaderManifest.Default));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ShaderErrorCodes.ShaderAlreadyExists);
    }

    [Fact]
    public async Task List_ShouldReturnShadersSortedById()
    {
        var workspace = new ShaderWorkspace(TestWorkspace.Create());
        await workspace.Create(new CreateShaderRequest("zebra", SampleShaders.Valid, ShaderManifest.Default));
        await workspace.Create(new CreateShaderRequest("alpha", SampleShaders.Valid, ShaderManifest.Default));

        var result = await workspace.List();

        result.Value.Select(x => x.Id).Should().Equal("alpha", "zebra");
    }
}
