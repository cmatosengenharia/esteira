using Esteira.Application.Publishing;
using Esteira.Domain.Enums;

namespace Esteira.UnitTests.Publishing;

public class NetworkSpecCatalogTests
{
    [Fact]
    public void Loads_every_network_from_contract()
    {
        var catalog = new NetworkSpecCatalog();
        foreach (var network in Enum.GetValues<SocialNetwork>())
            Assert.Equal(network, catalog.Get(network).Network);

        var shorts = catalog.Get(SocialNetwork.YouTubeShorts);
        Assert.Equal(AspectRatio.Vertical9x16, shorts.Aspect);
        Assert.Equal(180, shorts.MaxDurationSeconds);
        Assert.True(shorts.HasPublicApi);
        Assert.False(catalog.Get(SocialNetwork.Kwai).HasPublicApi);
        Assert.Equal(AspectRatio.Horizontal16x9, catalog.Get(SocialNetwork.YouTube).Aspect);
    }
}
