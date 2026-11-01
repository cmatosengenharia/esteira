using System.Text.Json;
using Esteira.Application.Contracts;
using Esteira.Domain.Enums;

namespace Esteira.Application.Publishing;

/// <summary>Limites de uma rede, carregados de docs/contracts/network-specs.json. 0 = sem limite ou não se aplica.</summary>
public sealed record NetworkSpec(
    SocialNetwork Network,
    AspectRatio Aspect,
    int MaxDurationSeconds,
    long MaxSizeBytes,
    int MaxCaptionChars,
    int MaxTitleChars,
    int RecommendedHashtags,
    bool HasPublicApi);

public sealed class NetworkSpecCatalog
{
    private readonly IReadOnlyDictionary<SocialNetwork, NetworkSpec> _specs;

    public NetworkSpecCatalog() : this(ContractResources.Read(ContractResources.NetworkSpecs)) { }

    public NetworkSpecCatalog(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var specs = new Dictionary<SocialNetwork, NetworkSpec>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name.StartsWith('_')) continue;
            if (!Enum.TryParse<SocialNetwork>(prop.Name, out var network))
                throw new InvalidOperationException($"Rede desconhecida em network-specs.json: {prop.Name}");
            var e = prop.Value;
            specs[network] = new NetworkSpec(
                network,
                AspectRatioExtensions.FromContract(e.GetProperty("aspect").GetString()!),
                e.GetProperty("maxDurationSeconds").GetInt32(),
                e.GetProperty("maxSizeBytes").GetInt64(),
                e.GetProperty("maxCaptionChars").GetInt32(),
                e.GetProperty("maxTitleChars").GetInt32(),
                e.GetProperty("recommendedHashtags").GetInt32(),
                e.GetProperty("hasPublicApi").GetBoolean());
        }
        _specs = specs;
    }

    public NetworkSpec Get(SocialNetwork network) => _specs.TryGetValue(network, out var s)
        ? s
        : throw new KeyNotFoundException($"Sem spec para {network} em network-specs.json");

    public IReadOnlyCollection<NetworkSpec> All => _specs.Values.ToList();
}
