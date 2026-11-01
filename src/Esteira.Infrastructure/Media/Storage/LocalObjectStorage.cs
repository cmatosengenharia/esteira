using Esteira.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Esteira.Infrastructure.Media.Storage;

/// <summary>Storage em disco (Fase 0). Chave "tenant/jobs/id/x.mp4" vira StorageRoot/tenant/jobs/id/x.mp4. Sem path traversal.</summary>
public sealed class LocalObjectStorage(IOptions<MediaOptions> options) : IObjectStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.StorageRoot);

    public async Task<string> PutAsync(string objectKey, Stream content, CancellationToken ct)
    {
        var path = Resolve(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
        await content.CopyToAsync(file, ct);
        return objectKey;
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct)
    {
        var path = Resolve(objectKey);
        if (!File.Exists(path)) throw new FileNotFoundException($"Objeto não encontrado: {objectKey}", path);
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true));
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken ct) => Task.FromResult(File.Exists(Resolve(objectKey)));

    public Task DeleteAsync(string objectKey, CancellationToken ct)
    {
        var path = Resolve(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<string> GetLocalPathAsync(string objectKey, CancellationToken ct)
    {
        var path = Resolve(objectKey);
        if (!File.Exists(path)) throw new FileNotFoundException($"Objeto não encontrado: {objectKey}", path);
        return Task.FromResult(path);
    }

    public string GetLocalPathForWrite(string objectKey)
    {
        var path = Resolve(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private string Resolve(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Contains("..") || Path.IsPathRooted(objectKey))
            throw new ArgumentException($"Chave inválida: {objectKey}", nameof(objectKey));
        var full = Path.GetFullPath(Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
            throw new ArgumentException($"Chave fora do storage: {objectKey}", nameof(objectKey));
        return full;
    }
}
