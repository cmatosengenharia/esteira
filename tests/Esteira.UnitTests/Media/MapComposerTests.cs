using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Maps;

namespace Esteira.UnitTests.Media;

public class MapComposerTests
{
    private static readonly MapPlace SaoPaulo = new("São Paulo", -23.55, -46.63);
    private static readonly MapPlace Curitiba = new("Curitiba", -25.43, -49.27);
    private static readonly MapPlace Recife = new("Recife", -8.05, -34.88);

    [Fact]
    public void Markers_appear_when_the_place_is_cited_and_pop_to_full_scale()
    {
        TranscriptWord[] words =
        [
            new("Começamos", 0.0, 0.5, 0.9, null), new("em", 0.6, 0.9, 0.9, null),
            new("são", 1.0, 1.3, 0.9, null), new("paulo,", 1.3, 1.8, 0.9, null),
            new("depois", 2.0, 2.5, 0.9, null), new("Curitiba", 3.0, 3.6, 0.9, null),
            new("e", 4.0, 4.2, 0.9, null), new("RECIFE.", 5.0, 5.6, 0.9, null),
        ];

        var map = MapComposer.Compose([SaoPaulo, Curitiba, Recife], words, durationSeconds: 8, fps: 10);

        Assert.Equal([1.0, 3.0, 5.0], map.Markers.Select(m => m.AppearAt));
        Assert.Equal(80, map.Frames.Count);
        // marcador 0 entra no quadro 10 (t = 1,0 s) e cresce até escala 1 em 0,4 s
        Assert.Equal(0, map.Frames[9].MarkerScales[0]);
        Assert.Equal(0, map.Frames[10].MarkerScales[0]);
        Assert.Equal(0.25, map.Frames[11].MarkerScales[0], 6);
        Assert.Equal(1, map.Frames[14].MarkerScales[0], 6);
        Assert.Equal(0, map.Frames[30].MarkerScales[1]);
        Assert.Equal(0.5, map.Frames[32].MarkerScales[1], 6);
        Assert.Equal(0, map.Frames[50].MarkerScales[2]);
        Assert.Equal(1, map.Frames[54].MarkerScales[2], 6);
        Assert.All(map.Frames[^1].MarkerScales, s => Assert.Equal(1, s, 6));
        Assert.All(map.Markers, m => Assert.InRange(m.X, 0, 1080));
        Assert.All(map.Markers, m => Assert.InRange(m.Y, 0, 1920));
    }

    [Fact]
    public void Places_not_cited_enter_at_equal_intervals()
    {
        var map = MapComposer.Compose([SaoPaulo, Curitiba], [], durationSeconds: 4, fps: 10);
        Assert.Equal([0.0, 2.0], map.Markers.Select(m => m.AppearAt));
    }

    [Fact]
    public void Single_place_is_centered_on_its_web_mercator_tile()
    {
        var map = MapComposer.Compose([SaoPaulo], [], durationSeconds: 2, maxZoom: 10);

        Assert.Equal(10, map.Zoom);
        Assert.Contains(map.Tiles, t => t is { Z: 10, X: 379, Y: 580 });
        Assert.Equal(540, map.Markers[0].X, 6);
        Assert.Equal(960, map.Markers[0].Y, 6);
    }

    [Fact]
    public void Composition_carries_osm_attribution_and_only_osm_tile_urls()
    {
        var map = MapComposer.Compose([SaoPaulo, Recife], [], durationSeconds: 2);

        Assert.Equal("© OpenStreetMap contributors", map.Attribution);
        Assert.NotEmpty(map.Tiles);
        Assert.All(map.Tiles, t => Assert.Equal($"https://tile.openstreetmap.org/{t.Z}/{t.X}/{t.Y}.png", t.Url));
    }
}
