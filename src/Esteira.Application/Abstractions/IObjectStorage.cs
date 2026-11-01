namespace Esteira.Application.Abstractions;

/// <summary>Storage de objetos por tenant. Chaves são relativas ("tenantId/jobs/jobId/source.mp4"); o caminho físico é da implementação.</summary>
public interface IObjectStorage
{
    Task<string> PutAsync(string objectKey, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
    /// <summary>Caminho local para ferramentas de linha de comando (ffmpeg, whisper). Implementações remotas baixam para um temporário.</summary>
    Task<string> GetLocalPathAsync(string objectKey, CancellationToken cancellationToken);
    /// <summary>Caminho local onde uma ferramenta pode gravar a saída que depois vira o objeto.</summary>
    string GetLocalPathForWrite(string objectKey);
}
