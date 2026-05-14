using System.Text.RegularExpressions;

namespace ShaderMcp.Core;

public static partial class SafeFileNames
{
    public static string Sanitize(string value)
    {
        var sanitized = SafeFileNameRegex().Replace(value, "-").Trim('-');
        while (sanitized.Contains("..", StringComparison.Ordinal))
            sanitized = sanitized.Replace("..", ".", StringComparison.Ordinal);

        sanitized = sanitized.Trim('.', '-');
        return string.IsNullOrWhiteSpace(sanitized) ? "frame" : sanitized[..Math.Min(80, sanitized.Length)];
    }

    [GeneratedRegex("[^A-Za-z0-9_.-]+", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFileNameRegex();
}
