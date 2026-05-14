using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShaderMcp.Core;

public sealed partial class ShaderWorkspace
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public ShaderWorkspace(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        ShadersPath = Path.Combine(RootPath, "shaders");
        ArtifactsPath = Path.Combine(RootPath, "artifacts");
        Directory.CreateDirectory(ShadersPath);
        Directory.CreateDirectory(ArtifactsPath);
    }

    public string RootPath { get; }

    public string ShadersPath { get; }

    public string ArtifactsPath { get; }

    public async Task<ShaderResult<ShaderDocument>> Create(CreateShaderRequest request, CancellationToken cancellationToken = default)
    {
        var idResult = ValidateId(request.Id);
        if (idResult.IsFailure)
            return ShaderResult<ShaderDocument>.Failure(idResult.ErrorCode, idResult.Error);

        if (string.IsNullOrWhiteSpace(request.Source))
            return ShaderResult<ShaderDocument>.Failure(ShaderErrorCodes.ValidationFailed, "SkSL source is required.");

        var manifestResult = ShaderManifestValidator.Validate(request.Manifest);
        if (manifestResult.IsFailure)
            return ShaderResult<ShaderDocument>.Failure(manifestResult.ErrorCode, manifestResult.Error);

        var shaderPath = GetShaderPath(request.Id);
        if (Directory.Exists(shaderPath) && !request.Overwrite)
            return ShaderResult<ShaderDocument>.Failure(ShaderErrorCodes.ShaderAlreadyExists, $"Shader '{request.Id}' already exists.");

        Directory.CreateDirectory(shaderPath);
        await File.WriteAllTextAsync(GetSourcePath(request.Id), request.Source, cancellationToken);
        await File.WriteAllTextAsync(GetManifestPath(request.Id), JsonSerializer.Serialize(request.Manifest, JsonOptions), cancellationToken);

        return ShaderResult<ShaderDocument>.Success(new ShaderDocument(request.Id, request.Source, request.Manifest));
    }

    public async Task<ShaderResult<ShaderDocument>> Update(UpdateShaderRequest request, CancellationToken cancellationToken = default)
    {
        var current = await Get(request.Id, cancellationToken);
        if (current.IsFailure)
            return current;

        var source = request.Source ?? current.Value.Source;
        var manifest = request.Manifest ?? current.Value.Manifest;

        if (string.IsNullOrWhiteSpace(source))
            return ShaderResult<ShaderDocument>.Failure(ShaderErrorCodes.ValidationFailed, "SkSL source is required.");

        var manifestResult = ShaderManifestValidator.Validate(manifest);
        if (manifestResult.IsFailure)
            return ShaderResult<ShaderDocument>.Failure(manifestResult.ErrorCode, manifestResult.Error);

        await File.WriteAllTextAsync(GetSourcePath(request.Id), source, cancellationToken);
        await File.WriteAllTextAsync(GetManifestPath(request.Id), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);

        return ShaderResult<ShaderDocument>.Success(new ShaderDocument(request.Id, source, manifest));
    }

    public async Task<ShaderResult<ShaderDocument>> Get(string id, CancellationToken cancellationToken = default)
    {
        var idResult = ValidateId(id);
        if (idResult.IsFailure)
            return ShaderResult<ShaderDocument>.Failure(idResult.ErrorCode, idResult.Error);

        var sourcePath = GetSourcePath(id);
        var manifestPath = GetManifestPath(id);
        if (!File.Exists(sourcePath) || !File.Exists(manifestPath))
            return ShaderResult<ShaderDocument>.Failure(ShaderErrorCodes.ShaderNotFound, $"Shader '{id}' does not exist.");

        try
        {
            var source = await File.ReadAllTextAsync(sourcePath, cancellationToken);
            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<ShaderManifest>(manifestJson, JsonOptions)
                ?? ShaderManifest.Default;
            return ShaderResult<ShaderDocument>.Success(new ShaderDocument(id, source, manifest));
        }
        catch (JsonException ex)
        {
            return ShaderResult<ShaderDocument>.Failure(ShaderErrorCodes.InvalidManifest, ex.Message);
        }
    }

    public async Task<ShaderResult<IReadOnlyList<ShaderSummary>>> List(CancellationToken cancellationToken = default)
    {
        var results = new List<ShaderSummary>();
        if (!Directory.Exists(ShadersPath))
            return ShaderResult<IReadOnlyList<ShaderSummary>>.Success(results);

        foreach (var directory in Directory.EnumerateDirectories(ShadersPath).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = Path.GetFileName(directory);
            var document = await Get(id, cancellationToken);
            if (document.IsSuccess)
                results.Add(new ShaderSummary(id, document.Value.Manifest.Name, document.Value.Manifest.Description));
        }

        return ShaderResult<IReadOnlyList<ShaderSummary>>.Success(results);
    }

    public string CreateArtifactPath(string id, string suffix)
    {
        var idResult = ValidateId(id);
        if (idResult.IsFailure)
            throw new ShaderValidationException(idResult.ErrorCode, idResult.Error);

        var directory = Path.Combine(ArtifactsPath, id);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{SafeFileNames.Sanitize(suffix)}");
    }

    private ShaderResult ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !IdRegex().IsMatch(id))
            return ShaderResult.Failure(ShaderErrorCodes.InvalidShaderId, "Shader id must match ^[a-z0-9][a-z0-9_-]{0,63}$.");

        var path = GetShaderPath(id);
        if (!path.StartsWith(ShadersPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return ShaderResult.Failure(ShaderErrorCodes.InvalidShaderId, "Shader id escapes the workspace.");

        return ShaderResult.Success();
    }

    private string GetShaderPath(string id) => Path.GetFullPath(Path.Combine(ShadersPath, id));

    private string GetSourcePath(string id) => Path.Combine(GetShaderPath(id), "shader.sksl");

    private string GetManifestPath(string id) => Path.Combine(GetShaderPath(id), "shader.json");

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();
}
