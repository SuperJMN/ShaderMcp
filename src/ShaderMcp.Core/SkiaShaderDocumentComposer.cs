using System.Text;

namespace ShaderMcp.Core;

public static class SkiaShaderDocumentComposer
{
    public static string Compose(string fragmentSource, ShaderManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(fragmentSource))
            throw new ShaderValidationException(ShaderErrorCodes.ValidationFailed, "SkSL source is required.");

        ShaderManifestValidator.EnsureValid(manifest);

        var sb = new StringBuilder();
        sb.AppendLine("uniform float2 resolution;");
        sb.AppendLine("uniform float time;");
        sb.AppendLine("uniform float frame;");
        sb.AppendLine("uniform float2 pointer;");
        sb.AppendLine("uniform shader inputTexture;");

        foreach (var parameter in manifest.Parameters)
            sb.AppendLine($"uniform float4 {parameter.Name};");

        sb.AppendLine();
        sb.AppendLine(fragmentSource.Trim());
        sb.AppendLine();
        sb.AppendLine("half4 main(float2 fragCoord) {");
        sb.AppendLine("    float2 uv = fragCoord / resolution;");
        sb.AppendLine("    return fragmentMain(fragCoord, uv);");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
