using FluentAssertions;
using ShaderMcp.Tool.Tools;

namespace ShaderMcp.Tests;

public sealed class ToolCatalogueTests
{
    [Fact]
    public void GetMarkdown_ShouldIncludeShaderWorkflowTools()
    {
        var markdown = ToolsCatalogue.GetMarkdown();

        markdown.Should().Contain("shader-mcp-create_shader");
        markdown.Should().Contain("shader-mcp-validate_shader");
        markdown.Should().Contain("shader-mcp-render_shader");
        markdown.Should().Contain("shader-mcp-run_shader_tests");
        markdown.Should().Contain("shader-mcp-render_shader_sequence");
    }
}
