namespace ShaderMcp.Tool;

public static class ShaderWorkspaceRoot
{
    public const string EnvironmentVariableName = "SHADER_MCP_WORKSPACE";

    public static string ResolveDefault() =>
        ResolveDefault(
            Environment.GetEnvironmentVariable,
            Environment.GetFolderPath,
            Path.GetTempPath());

    public static string ResolveDefault(
        Func<string, string?> getEnvironmentVariable,
        Func<Environment.SpecialFolder, string> getFolderPath,
        string tempPath)
    {
        var explicitRoot = getEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return Path.GetFullPath(explicitRoot);
        }

        var localData = getFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localData))
        {
            return Path.Combine(localData, "ShaderMcp");
        }

        return Path.Combine(tempPath, "ShaderMcp");
    }
}
