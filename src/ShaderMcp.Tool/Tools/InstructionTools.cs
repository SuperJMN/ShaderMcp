using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ShaderMcp.Tool.Tools;

[McpServerToolType]
public sealed class InstructionTools
{
    [McpServerTool(Name = "instructions"), Description("Get Shader MCP usage instructions. Use page='tools' for the canonical tool catalogue.")]
    public static string Instructions([Description("readme or tools")] string page = "readme")
    {
        if (string.Equals(page, "tools", StringComparison.OrdinalIgnoreCase))
            return ToolsCatalogue.GetMarkdown();

        return """
            # Shader MCP

            Local MCP server for creating, validating, rendering, and regression-testing native Skia RuntimeEffect fragment shaders.

            Recommended workflow:
            1. `create_shader` with an id, SkSL fragment function, and optional manifest JSON.
            2. `validate_shader` to compile the native runtime effect.
            3. `render_shader` to capture a PNG and pixel statistics.
            4. `render_shader_sequence` to export deterministic PNG frame sequences.
            5. `run_shader_tests` for manifest-defined smoke or visual checks.

            Shader source must define:

            ```sksl
            half4 fragmentMain(float2 fragCoord, float2 uv) {
                return half4(uv.x, uv.y, 0.5, 1.0);
            }
            ```

            Built-in uniforms: `resolution`, `time`, `frame`, `pointer`, and `inputTexture`.
            Manifest parameters are injected as `uniform float4 <name>`.
            """;
    }
}
