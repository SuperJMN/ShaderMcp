using FluentAssertions;
using ShaderMcp.Tool;

namespace ShaderMcp.Tests;

public sealed class ShaderWorkspaceRootTests
{
    [Fact]
    public void ResolveDefault_WhenEnvironmentVariableIsSet_ShouldUseExplicitRoot()
    {
        var result = ShaderWorkspaceRoot.ResolveDefault(
            key => key == ShaderWorkspaceRoot.EnvironmentVariableName ? "/tmp/custom-shaders" : null,
            _ => "/tmp/local-data",
            "/tmp");

        result.Should().Be(Path.GetFullPath("/tmp/custom-shaders"));
    }

    [Fact]
    public void ResolveDefault_WhenEnvironmentVariableIsMissing_ShouldUseLocalApplicationData()
    {
        var result = ShaderWorkspaceRoot.ResolveDefault(
            _ => null,
            _ => "/tmp/local-data",
            "/tmp");

        result.Should().Be(Path.Combine("/tmp/local-data", "ShaderMcp"));
    }

    [Fact]
    public void ResolveDefault_WhenNoUserDataFolderExists_ShouldFallBackToTemp()
    {
        var result = ShaderWorkspaceRoot.ResolveDefault(
            _ => null,
            _ => "",
            "/tmp");

        result.Should().Be(Path.Combine("/tmp", "ShaderMcp"));
    }
}
