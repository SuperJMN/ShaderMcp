using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using ModelContextProtocol.Server;

namespace ShaderMcp.Tool.Tools;

public static class ToolsCatalogue
{
    private const string ToolNamePrefix = "shader-mcp-";
    private static readonly Lazy<string> CachedMarkdown = new(BuildMarkdown);
    private static readonly Lazy<IReadOnlyList<ToolEntry>> CachedTools = new(DiscoverTools);

    public static string GetMarkdown() => CachedMarkdown.Value;

    public static IReadOnlyList<ToolEntry> GetTools() => CachedTools.Value;

    public sealed record ParameterInfoEntry(string Name, string Type, bool Required, string? Description);

    public sealed record ToolEntry(
        string Name,
        string PrefixedName,
        string Purpose,
        IReadOnlyList<ParameterInfoEntry> Parameters);

    private static IReadOnlyList<ToolEntry> DiscoverTools()
    {
        var assembly = typeof(ToolsCatalogue).Assembly;
        var entries = new List<ToolEntry>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr is null)
                    continue;

                var name = !string.IsNullOrWhiteSpace(toolAttr.Name)
                    ? toolAttr.Name!
                    : ToKebabCase(method.Name);

                var purpose = ExtractFirstLine(method.GetCustomAttribute<DescriptionAttribute>()?.Description);
                var parameters = method.GetParameters()
                    .Where(p => !IsInfrastructureParameter(p))
                    .Select(p => new ParameterInfoEntry(
                        p.Name ?? "?",
                        FormatType(p.ParameterType),
                        Required: !p.HasDefaultValue && !IsNullable(p),
                        p.GetCustomAttribute<DescriptionAttribute>()?.Description))
                    .ToList();

                entries.Add(new ToolEntry(name, ToolNamePrefix + name, purpose, parameters));
            }
        }

        return entries.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
    }

    private static string BuildMarkdown()
    {
        var tools = GetTools();
        var sb = new StringBuilder();

        sb.AppendLine("# Shader MCP Tool Catalogue");
        sb.AppendLine();
        sb.AppendLine($"<!-- Generated at server start from {tools.Count} registered tools. -->");
        sb.AppendLine();
        sb.AppendLine("| Tool name | Purpose | Required params |");
        sb.AppendLine("|---|---|---|");
        foreach (var tool in tools)
        {
            var required = tool.Parameters.Where(p => p.Required).ToList();
            var requiredText = required.Count == 0
                ? "_(none)_"
                : string.Join(", ", required.Select(p => $"`{p.Name}: {p.Type}`"));
            sb.Append("| `").Append(tool.PrefixedName).Append("` | ")
                .Append(EscapePipe(tool.Purpose)).Append(" | ")
                .Append(requiredText).AppendLine(" |");
        }

        sb.AppendLine();
        sb.AppendLine("## Native SkSL Contract");
        sb.AppendLine();
        sb.AppendLine("Write a fragment function named `fragmentMain(float2 fragCoord, float2 uv) -> half4`.");
        sb.AppendLine("The runner wraps it as a Skia RuntimeEffect with deterministic built-in uniforms: `resolution`, `time`, `frame`, `pointer`, and `inputTexture`.");
        sb.AppendLine("Parameters declared in `shader.json` are exposed as `uniform float4 <name>`.");
        sb.AppendLine();
        sb.AppendLine("## Recommended Call Order");
        sb.AppendLine();
        sb.AppendLine("- Create or edit: `create_shader` / `update_shader`.");
        sb.AppendLine("- Compile check: `validate_shader`.");
        sb.AppendLine("- Visual smoke: `render_shader`.");
        sb.AppendLine("- Frame sequences: `render_shader_sequence`.");
        sb.AppendLine("- Regression checks: `run_shader_tests`.");

        return sb.ToString();
    }

    private static bool IsInfrastructureParameter(ParameterInfo p)
    {
        var t = p.ParameterType;
        if (t == typeof(CancellationToken)) return true;
        var ns = t.Namespace ?? string.Empty;
        if (ns.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)) return true;
        if (ns.StartsWith("ModelContextProtocol", StringComparison.Ordinal)) return true;
        if (ns.StartsWith("ShaderMcp.Core", StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool IsNullable(ParameterInfo p)
    {
        if (p.ParameterType.IsValueType)
            return Nullable.GetUnderlyingType(p.ParameterType) is not null;

        var ctx = new NullabilityInfoContext();
        return ctx.Create(p).WriteState == NullabilityState.Nullable;
    }

    private static string FormatType(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying is not null) return FormatType(underlying) + "?";

        return t switch
        {
            _ when t == typeof(string) => "string",
            _ when t == typeof(int) => "int",
            _ when t == typeof(bool) => "bool",
            _ when t == typeof(double) => "double",
            _ when t == typeof(float) => "float",
            _ => t.Name,
        };
    }

    private static string ExtractFirstLine(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        var idx = description.IndexOf('\n');
        return (idx >= 0 ? description[..idx] : description).Trim();
    }

    private static string ToKebabCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 8);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('-');
                sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string EscapePipe(string s) => s.Replace("|", "\\|");
}
