using System.Globalization;
using System.Text;
using Esteira.Application.Abstractions;

namespace Esteira.Infrastructure.Media.Maps;

public sealed record MapPlace(string Name, double Lat, double Lon);

public sealed record MapTile(int Z, int X, int Y, string Url);

/// <summary>Marcador em pixels do quadro e instante (s) em que entra.</summary>
public sealed record MapMarker(string Name, double X, double Y, double AppearAt);

/// <summary>Escala de cada marcador (na ordem de <see cref="MapComposition.Markers"/>) no quadro, de 0 a 1.</summary>
public sealed record MapFrame(int Index, IReadOnlyList<double> MarkerScales);

public sealed record MapComposition(int Zoom, IReadOnlyList<MapTile> Tiles, IReadOnlyList<MapMarker> Markers, IReadOnlyList<MapFrame> Frames, string Attribution);

/// <summary>
/// Estilo 2 (mapa animado): fundo estático de tiles OpenStreetMap (Web Mercator, tiles de 256 px) com um marcador por
/// local que entra quando o local é citado na transcrição (ou em intervalos iguais se não for) e cresce até escala 1
/// em popSeconds. Cálculo puro: não baixa tiles; o render usa as URLs e a atribuição (ODbL, ver THIRD_PARTY_NOTICES.md).
/// </summary>
public static class MapComposer
{
    public const string Attribution = "© OpenStreetMap contributors";
    private const int TileSize = 256;

    public static MapComposition Compose(IReadOnlyList<MapPlace> places, IReadOnlyList<TranscriptWord> words, double durationSeconds,
        int width = 1080, int height = 1920, int fps = 30, double popSeconds = 0.4, int maxZoom = 14)
    {
        var zoom = maxZoom;
        while (zoom > 0)
        {
            var pts = places.Select(p => WorldPixel(p, zoom)).ToList();
            if (pts.Max(p => p.X) - pts.Min(p => p.X) <= width * 0.8 && pts.Max(p => p.Y) - pts.Min(p => p.Y) <= height * 0.8) break;
            zoom--;
        }

        var world = places.Select(p => WorldPixel(p, zoom)).ToList();
        var centerX = (world.Min(p => p.X) + world.Max(p => p.X)) / 2;
        var centerY = (world.Min(p => p.Y) + world.Max(p => p.Y)) / 2;
        var left = centerX - width / 2.0;
        var top = centerY - height / 2.0;

        var markers = places.Select((p, i) => new MapMarker(p.Name, world[i].X - left, world[i].Y - top,
            FindMention(p.Name, words) ?? durationSeconds * i / places.Count)).ToList();

        var n = 1 << zoom;
        var tiles = new List<MapTile>();
        for (var ty = (int)Math.Floor(top / TileSize); ty <= (int)Math.Floor((top + height - 1) / TileSize); ty++)
        {
            if (ty < 0 || ty >= n) continue;
            for (var tx = (int)Math.Floor(left / TileSize); tx <= (int)Math.Floor((left + width - 1) / TileSize); tx++)
            {
                var x = ((tx % n) + n) % n;
                tiles.Add(new MapTile(zoom, x, ty, $"https://tile.openstreetmap.org/{zoom}/{x}/{ty}.png"));
            }
        }

        var frames = Enumerable.Range(0, (int)Math.Ceiling(durationSeconds * fps)).Select(i =>
        {
            var t = (double)i / fps;
            return new MapFrame(i, markers.Select(m => Math.Clamp((t - m.AppearAt) / popSeconds, 0, 1)).ToList());
        }).ToList();

        return new MapComposition(zoom, tiles, markers, frames, Attribution);
    }

    private static (double X, double Y) WorldPixel(MapPlace place, int zoom)
    {
        var size = TileSize * (double)(1 << zoom);
        var lat = place.Lat * Math.PI / 180;
        var x = (place.Lon + 180) / 360 * size;
        var y = (1 - Math.Asinh(Math.Tan(lat)) / Math.PI) / 2 * size;
        return (x, y);
    }

    /// <summary>Início da primeira sequência de palavras igual ao nome do local (sem acento, caixa e pontuação).</summary>
    private static double? FindMention(string name, IReadOnlyList<TranscriptWord> words)
    {
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Normalize).ToList();
        for (var i = 0; i + tokens.Count <= words.Count; i++)
        {
            if (tokens.Select((tok, k) => Normalize(words[i + k].Word) == tok).All(match => match)) return words[i].Start;
        }
        return null;
    }

    private static string Normalize(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
