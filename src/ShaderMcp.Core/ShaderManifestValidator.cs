using System.Text.RegularExpressions;

namespace ShaderMcp.Core;

public static partial class ShaderManifestValidator
{
    public static ShaderResult Validate(ShaderManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Name))
            return ShaderResult.Failure(ShaderErrorCodes.InvalidManifest, "Manifest name is required.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in manifest.Parameters)
        {
            if (!IsShaderIdentifier(parameter.Name))
                return ShaderResult.Failure(ShaderErrorCodes.InvalidParameterName, $"Parameter '{parameter.Name}' is not a valid shader identifier.");

            if (!names.Add(parameter.Name))
                return ShaderResult.Failure(ShaderErrorCodes.InvalidManifest, $"Parameter '{parameter.Name}' is duplicated.");

            if (parameter.DefaultValue.Count is < 1 or > 4)
                return ShaderResult.Failure(ShaderErrorCodes.InvalidManifest, $"Parameter '{parameter.Name}' must have between 1 and 4 default values.");
        }

        return ShaderResult.Success();
    }

    public static void EnsureValid(ShaderManifest manifest)
    {
        var result = Validate(manifest);
        if (result.IsFailure)
            throw new ShaderValidationException(result.ErrorCode, result.Error);
    }

    public static bool IsShaderIdentifier(string value) => IdentifierRegex().IsMatch(value);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();
}
