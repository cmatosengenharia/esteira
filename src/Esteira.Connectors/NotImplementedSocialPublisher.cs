using Esteira.Application.Publishing;
using Esteira.Domain.Enums;

namespace Esteira.Connectors;

/// <summary>Placeholder registrado para toda rede até o conector real existir (YouTube é o primeiro, Fase 1).</summary>
public sealed class NotImplementedSocialPublisher(SocialNetwork network) : ISocialPublisher
{
    public SocialNetwork Network { get; } = network;
    public IReadOnlyCollection<PublishMode> SupportedModes => [];

    public IReadOnlyList<string> Validate(PublishRequest request, NetworkSpec spec)
        => throw new NotImplementedException($"Conector de {Network} ainda não existe. Conectores entram a partir da Fase 1 (YouTube primeiro, modos Api e Assisted).");

    public Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException($"Conector de {Network} ainda não existe. Conectores entram a partir da Fase 1 (YouTube primeiro, modos Api e Assisted).");
}
