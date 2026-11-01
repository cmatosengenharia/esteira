using Esteira.Domain.Entities;
using Esteira.Domain.Enums;

namespace Esteira.Application.Publishing;

public sealed record PublishRequest(
    VideoJob Job,
    SocialAccount Account,
    string MasterObjectKey,
    string Title,
    string Caption,
    IReadOnlyList<string> Hashtags,
    PublishMode Mode);

public sealed record PublishResult(
    PublishMode Mode,
    bool Succeeded,
    // Id ou URL do post quando Mode = Api.
    string? ExternalPostId,
    // Quando Mode = Assisted: pacote pronto (arquivo, copy, lembrete) para o humano postar.
    AssistedPackage? Assisted,
    string? Error);

public sealed record AssistedPackage(string FileObjectKey, string Copy, string Instructions, DateTimeOffset RemindAt);

/// <summary>Um conector por rede. Precisa suportar os dois modos; Assisted é obrigatório, Api quando a rede tem API pública.</summary>
public interface ISocialPublisher
{
    SocialNetwork Network { get; }
    IReadOnlyCollection<PublishMode> SupportedModes { get; }
    /// <summary>Confere limites da <see cref="NetworkSpec"/> antes de publicar; retorna lista de violações (vazia = ok).</summary>
    IReadOnlyList<string> Validate(PublishRequest request, NetworkSpec spec);
    Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken cancellationToken);
}
