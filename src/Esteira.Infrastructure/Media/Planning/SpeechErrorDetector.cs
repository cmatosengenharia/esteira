using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Audio;

namespace Esteira.Infrastructure.Media.Planning;

/// <summary>
/// Erros de fala a partir da transcrição por palavra: tomada repetida (n-grama de 4+ palavras dito de novo em menos de 10 s:
/// a primeira é o erro, fica a segunda) e hesitação isolada ("é…", "hã", "tipo" com pausa > 0,6 s antes e depois).
/// Custo: 0 tokens.
/// </summary>
public static class SpeechErrorDetector
{
    private const int MinRepeatWords = 4;
    private const double RepeatWindowSeconds = 10;
    private const double HesitationPauseSeconds = 0.6;
    private const double MinClipSeconds = 0.15;
    private static readonly HashSet<string> Hesitations = ["é", "hã", "tipo"];

    /// <summary>Trechos a remover, em ordem, com o motivo ("repetição" ou "hesitação").</summary>
    public static IReadOnlyList<(TimeRange Range, string Kind)> DetectCuts(IReadOnlyList<TranscriptWord> words)
    {
        var norm = words.Select(w => Normalize(w.Word)).ToList();
        var cuts = new List<(TimeRange Range, string Kind)>();

        var i = 0;
        while (i + MinRepeatWords <= words.Count)
        {
            var next = i + 1;
            for (var j = i + MinRepeatWords; j + MinRepeatWords <= words.Count && words[j].Start - words[i].Start < RepeatWindowSeconds; j++)
            {
                if (!Enumerable.Range(0, MinRepeatWords).All(k => norm[i + k].Length > 0 && norm[i + k] == norm[j + k])) continue;
                cuts.Add((new TimeRange(words[i].Start, words[j].Start), "repetição"));
                next = j; // continua da segunda tomada, que fica
                break;
            }
            i = next;
        }

        for (var k = 0; k < words.Count; k++)
        {
            if (!Hesitations.Contains(norm[k])) continue;
            var before = k == 0 ? double.MaxValue : words[k].Start - words[k - 1].End;
            var after = k == words.Count - 1 ? double.MaxValue : words[k + 1].Start - words[k].End;
            if (before > HesitationPauseSeconds && after > HesitationPauseSeconds)
                cuts.Add((new TimeRange(words[k].Start, words[k].End), "hesitação"));
        }

        return cuts.OrderBy(c => c.Range.In).ToList();
    }

    /// <summary>Tira os cortes dos trechos mantidos; pedaços menores que 0,15 s são descartados.</summary>
    public static IReadOnlyList<TimeRange> Subtract(IReadOnlyList<TimeRange> keep, IReadOnlyList<TimeRange> cuts)
    {
        var ordered = cuts.OrderBy(c => c.In).ToList();
        var result = new List<TimeRange>();
        foreach (var r in keep)
        {
            var start = r.In;
            foreach (var c in ordered)
            {
                if (c.Out <= start || c.In >= r.Out) continue;
                if (c.In > start) result.Add(new TimeRange(start, c.In));
                start = Math.Max(start, c.Out);
            }
            if (start < r.Out) result.Add(new TimeRange(start, r.Out));
        }
        return result.Where(r => r.Duration >= MinClipSeconds)
            .Select(r => new TimeRange(Math.Round(r.In, 3), Math.Round(r.Out, 3))).ToList();
    }

    private static string Normalize(string word) => word.Trim().Trim('.', ',', '!', '?', ';', ':', '…', '"').ToLowerInvariant();
}
