using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Audio;

namespace Esteira.Infrastructure.Media.Captions;

/// <summary>Converte tempos do vídeo original para a linha do tempo do vídeo cortado (soma dos trechos mantidos).</summary>
public static class TimelineMapper
{
    /// <summary>Palavras fora dos trechos mantidos são descartadas; as demais recebem tempos relativos à saída.</summary>
    public static IReadOnlyList<TranscriptWord> Remap(IReadOnlyList<TranscriptWord> words, IReadOnlyList<TimeRange> keep)
    {
        var result = new List<TranscriptWord>();
        double offset = 0;
        foreach (var range in keep)
        {
            foreach (var w in words)
            {
                var mid = (w.Start + w.End) / 2;
                if (mid < range.In || mid >= range.Out) continue;
                var start = Math.Max(range.In, w.Start) - range.In + offset;
                var end = Math.Min(range.Out, w.End) - range.In + offset;
                result.Add(w with { Start = Math.Round(start, 3), End = Math.Round(Math.Max(end, start + 0.05), 3) });
            }
            offset += range.Duration;
        }
        return result;
    }

    public static double TotalDuration(IReadOnlyList<TimeRange> keep) => Math.Round(keep.Sum(r => r.Duration), 3);
}
