using System.Globalization;
using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Audio;

namespace Esteira.Infrastructure.Media.Planning;

/// <summary>Corte curto candidato da EDL (`clips[]` do contrato). Tempos no vídeo original.</summary>
public sealed record EdlClip(string Title, double In, double Out, double Score, string? HookSentence);

/// <summary>Quantos shorts por vídeo e em que faixa de duração (segundos de vídeo já cortado).</summary>
public sealed record ShortsOptions(int MaxClips = 3, double MinSeconds = 30, double MaxSeconds = 60, double MinSourceKeptSeconds = 45);

/// <summary>Frase completa da transcrição dentro dos trechos mantidos: termina em pontuação, em pausa ou num corte.</summary>
public sealed record Sentence(double Start, double End, string Text, int WordCount);

/// <summary>
/// Escolhe até N trechos de 30–60 s do vídeo falado para virar shorts, sem LLM: janelas de frases completas,
/// pontuadas por gancho (pergunta, número, palavra de abertura) e densidade de fala. Tempos no vídeo original;
/// a duração conta só o que fica depois dos cortes (`keep`). Custo: 0 tokens.
/// </summary>
public static class ShortsPlanner
{
    private static readonly char[] SentenceEnd = ['.', '!', '?', '…'];
    private static readonly string[] HookOpeners =
        ["como", "por que", "porque", "nunca", "erro", "segredo", "ninguém", "você", "olha", "o que", "quanto", "dica", "pare", "cuidado", "a verdade"];

    public static IReadOnlyList<EdlClip> Plan(IReadOnlyList<TranscriptWord> words, IReadOnlyList<TimeRange> keep, EdlParams p, ShortsOptions o)
    {
        if (o.MaxClips <= 0 || keep.Count == 0 || KeptDuration(keep, keep[0].In, keep[^1].Out) < o.MinSourceKeptSeconds) return [];
        var sentences = Sentences(words, keep, p.SplitGapSeconds);
        if (sentences.Count == 0) return [];

        var target = (o.MinSeconds + o.MaxSeconds) / 2;
        var candidates = new List<(TimeRange Range, double Score, Sentence First, double Kept)>();
        for (var i = 0; i < sentences.Count; i++)
        {
            (int j, double kept, double dist)? best = null;
            for (var j = i; j < sentences.Count; j++)
            {
                var kept = KeptDuration(keep, sentences[i].Start, sentences[j].End);
                if (kept > o.MaxSeconds) break;
                if (kept < o.MinSeconds) continue;
                var dist = Math.Abs(kept - target);
                if (best is null || dist < best.Value.dist) best = (j, kept, dist);
            }
            if (best is null) continue;
            var range = new TimeRange(Math.Round(Math.Max(0, sentences[i].Start - p.LeadSeconds), 3), Math.Round(sentences[best.Value.j].End + p.TrailSeconds, 3));
            var wordsInWindow = sentences.Skip(i).Take(best.Value.j - i + 1).Sum(s => s.WordCount);
            candidates.Add((range, Score(sentences[i], wordsInWindow / best.Value.kept), sentences[i], best.Value.kept));
        }

        var chosen = new List<(TimeRange Range, double Score, Sentence First, double Kept)>();
        foreach (var c in candidates.OrderByDescending(c => c.Score).ThenBy(c => c.Range.In))
        {
            if (chosen.Count >= o.MaxClips) break;
            if (chosen.Any(x => x.Range.In < c.Range.Out && c.Range.In < x.Range.Out)) continue; // sobrepõe um já escolhido
            chosen.Add(c);
        }

        return chosen.OrderBy(c => c.Range.In)
            .Select(c => new EdlClip(Title(c.First.Text), c.Range.In, c.Range.Out, c.Score, c.First.Text))
            .ToList();
    }

    /// <summary>Frases a partir das palavras que caem dentro de `keep`; corte, pausa >= splitGap ou pontuação final fecham a frase.</summary>
    public static IReadOnlyList<Sentence> Sentences(IReadOnlyList<TranscriptWord> words, IReadOnlyList<TimeRange> keep, double splitGapSeconds)
    {
        var inside = words.Where(w => RangeIndex(keep, (w.Start + w.End) / 2) >= 0).OrderBy(w => w.Start).ToList();
        var result = new List<Sentence>();
        var buffer = new List<TranscriptWord>();
        for (var i = 0; i < inside.Count; i++)
        {
            buffer.Add(inside[i]);
            var last = i == inside.Count - 1;
            var ends = inside[i].Word.TrimEnd().LastOrDefault() is var ch && SentenceEnd.Contains(ch);
            var breaks = !last && (inside[i + 1].Start - inside[i].End >= splitGapSeconds
                                   || RangeIndex(keep, (inside[i + 1].Start + inside[i + 1].End) / 2) != RangeIndex(keep, (inside[i].Start + inside[i].End) / 2));
            if (last || ends || breaks)
            {
                result.Add(new Sentence(buffer[0].Start, buffer[^1].End, string.Join(' ', buffer.Select(w => w.Word.Trim())), buffer.Count));
                buffer.Clear();
            }
        }
        return result;
    }

    /// <summary>Trechos de `keep` que caem dentro do clipe (interseção), para o render do short.</summary>
    public static IReadOnlyList<TimeRange> ClipKeep(IReadOnlyList<TimeRange> keep, TimeRange clip)
        => keep.Where(r => r.Out > clip.In && r.In < clip.Out)
            .Select(r => new TimeRange(Math.Max(r.In, clip.In), Math.Min(r.Out, clip.Out)))
            .Where(r => r.Duration > 0.05).ToList();

    /// <summary>Segundos mantidos entre dois instantes do vídeo original.</summary>
    public static double KeptDuration(IReadOnlyList<TimeRange> keep, double from, double to)
        => ClipKeep(keep, new TimeRange(from, to)).Sum(r => r.Duration);

    private static double Score(Sentence hook, double wordsPerSecond)
    {
        var text = hook.Text.ToLowerInvariant();
        double score = 40;
        if (hook.WordCount <= 14) score += 10;
        if (text.Contains('?')) score += 15;
        if (text.Any(char.IsDigit)) score += 10;
        if (HookOpeners.Any(k => text.StartsWith(k, StringComparison.Ordinal))) score += 15;
        if (wordsPerSecond >= 2.5) score += 10; else if (wordsPerSecond >= 2.0) score += 5;
        return Math.Clamp(Math.Round(score, 1), 0, 100);
    }

    private static string Title(string sentence)
    {
        var t = sentence.TrimEnd(SentenceEnd).Trim();
        if (t.Length <= 60) return t;
        var cut = t.LastIndexOf(' ', 59);
        return (cut > 20 ? t[..cut] : t[..60]) + "…";
    }

    private static int RangeIndex(IReadOnlyList<TimeRange> keep, double t)
    {
        for (var i = 0; i < keep.Count; i++) if (t >= keep[i].In && t < keep[i].Out) return i;
        return -1;
    }
}
