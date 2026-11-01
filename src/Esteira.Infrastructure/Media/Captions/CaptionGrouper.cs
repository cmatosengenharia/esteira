using Esteira.Application.Abstractions;

namespace Esteira.Infrastructure.Media.Captions;

public sealed record CaptionPhrase(double Start, double End, string Text, IReadOnlyList<TranscriptWord> Words);

/// <summary>
/// Agrupa palavras em frases curtas no estilo TikTok. Regra e padrão (1200 ms) seguem o
/// createTikTokStyleCaptions de @remotion/captions (MIT); implementação própria. Registro em THIRD_PARTY_NOTICES.md.
/// Nova frase quando: a janela desde o início da frase passa de combineWithinMs; o intervalo entre palavras passa de
/// splitGapSeconds; a linha estouraria maxCharsPerLine; ou a palavra anterior termina frase (. ! ?).
/// </summary>
public static class CaptionGrouper
{
    public static IReadOnlyList<CaptionPhrase> Group(IReadOnlyList<TranscriptWord> words, int combineWithinMs = 1200, int maxCharsPerLine = 42, double splitGapSeconds = 0.6)
    {
        var phrases = new List<CaptionPhrase>();
        var current = new List<TranscriptWord>();
        var currentChars = 0;

        foreach (var word in words)
        {
            if (current.Count > 0)
            {
                var first = current[0];
                var prev = current[^1];
                var breaks =
                    (word.Start - first.Start) * 1000 >= combineWithinMs
                    || word.Start - prev.End > splitGapSeconds
                    || currentChars + 1 + word.Word.Length > maxCharsPerLine
                    || EndsSentence(prev.Word);
                if (breaks)
                {
                    phrases.Add(Build(current));
                    current = [];
                    currentChars = 0;
                }
            }
            current.Add(word);
            currentChars += (currentChars == 0 ? 0 : 1) + word.Word.Length;
        }
        if (current.Count > 0) phrases.Add(Build(current));

        // Estende cada frase até a próxima (sem buraco de tela vazia), com mínimo de 0.5 s.
        for (var i = 0; i < phrases.Count; i++)
        {
            var p = phrases[i];
            var minEnd = p.Start + 0.5;
            var nextStart = i + 1 < phrases.Count ? phrases[i + 1].Start : double.MaxValue;
            var end = Math.Min(nextStart, Math.Max(p.End, minEnd));
            phrases[i] = p with { End = Math.Round(end, 3) };
        }
        return phrases;
    }

    private static CaptionPhrase Build(List<TranscriptWord> words)
        => new(words[0].Start, words[^1].End, string.Join(' ', words.Select(w => w.Word)), words.ToList());

    private static bool EndsSentence(string word) => word.Length > 0 && word[^1] is '.' or '!' or '?';
}
