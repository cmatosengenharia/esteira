using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Audio;
using Esteira.Infrastructure.Media.Captions;
using Esteira.Infrastructure.Media.Planning;

namespace Esteira.UnitTests.Media;

public class CaptionTests
{
    /// <summary>Transcrição fixa: 12 palavras, uma pausa longa depois de "esteira." e uma frase final curta.</summary>
    public static IReadOnlyList<TranscriptWord> Fixed() =>
    [
        new("Olá", 0.00, 0.30, 0.9, null), new("pessoal", 0.35, 0.80, 0.9, null), new("hoje", 0.85, 1.10, 0.9, null),
        new("vamos", 1.15, 1.45, 0.9, null), new("falar", 1.50, 1.85, 0.9, null), new("da", 1.90, 2.00, 0.9, null),
        new("esteira.", 2.05, 2.60, 0.9, null),
        new("Ela", 4.00, 4.20, 0.9, null), new("corta", 4.25, 4.55, 0.9, null), new("silêncio", 4.60, 5.10, 0.9, null),
        new("e", 5.15, 5.20, 0.9, null), new("legenda.", 5.25, 5.80, 0.9, null),
    ];

    [Fact]
    public void Groups_words_in_1200ms_windows_and_breaks_on_pause_and_sentence_end()
    {
        var phrases = CaptionGrouper.Group(Fixed());
        var texts = phrases.Select(p => p.Text).ToList();

        Assert.Equal(["Olá pessoal hoje", "vamos falar da esteira.", "Ela corta silêncio", "e legenda."], texts);
        Assert.Equal(0.0, phrases[0].Start);
        Assert.Equal(1.15, phrases[0].End); // estendida até a próxima frase
        Assert.Equal(2.60, phrases[1].End);  // não invade a pausa longa
        Assert.All(phrases, p => Assert.True(p.Text.Length <= 42));
    }

    [Fact]
    public void Max_chars_per_line_splits_long_phrases()
    {
        var words = Enumerable.Range(0, 10).Select(i => new TranscriptWord("palavra", i * 0.1, i * 0.1 + 0.08, 1, null)).ToList();
        var phrases = CaptionGrouper.Group(words, combineWithinMs: 60_000, maxCharsPerLine: 20);
        Assert.All(phrases, p => Assert.True(p.Text.Length <= 20));
        Assert.Equal(4, phrases.Count); // "palavra palavra" (15) cabe, 3 não (23)
    }

    [Fact]
    public void Timeline_remap_drops_cut_words_and_shifts_the_rest()
    {
        var keep = new List<TimeRange> { new(0, 2.7), new(3.9, 5.9) };
        var remapped = TimelineMapper.Remap(Fixed(), keep);
        Assert.Equal(12, remapped.Count);
        var ela = remapped.Single(w => w.Word == "Ela");
        Assert.Equal(2.7 + (4.0 - 3.9), ela.Start, 3);

        var cut = TimelineMapper.Remap(Fixed(), [new TimeRange(0, 2.7)]);
        Assert.Equal(7, cut.Count);
        Assert.Equal(2.7, TimelineMapper.TotalDuration([new TimeRange(0, 2.7)]));
    }

    [Fact]
    public void Ass_output_has_styles_and_one_dialogue_per_phrase()
    {
        var phrases = CaptionGrouper.Group(Fixed());
        var ass = AssWriter.Write(phrases, new CaptionStyle(), 1080, 1920);
        Assert.Contains("PlayResX: 1080", ass);
        Assert.Contains("Style: Default,Arial,64,&H00FFFFFF,&H0000D4FF,", ass);
        Assert.Equal(phrases.Count, ass.Split('\n').Count(l => l.StartsWith("Dialogue:")));
        Assert.Contains("Dialogue: 0,0:00:00.00,0:00:01.15,Default,,0,0,0,,Olá pessoal hoje", ass);
        Assert.Equal("&H0000D4FF", AssWriter.ToAssColor("#FFD400"));
        Assert.Equal("1:01:01.50", AssWriter.Time(3661.5));
    }
}
