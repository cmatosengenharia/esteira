using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Audio;
using Esteira.Infrastructure.Media.Planning;

namespace Esteira.UnitTests.Media;

public class SpeechErrorDetectorTests
{
    /// <summary>Primeira tomada errada (0–1.5 s), recomeço em 1.8 s, "é…" isolado em 4.6 s e um "é" verbo no meio da frase.</summary>
    public static IReadOnlyList<TranscriptWord> WithErrors() =>
    [
        new("Vamos", 0.00, 0.30, 0.9, null), new("falar", 0.35, 0.60, 0.9, null), new("da", 0.65, 0.75, 0.9, null),
        new("esteira", 0.80, 1.20, 0.9, null), new("hoje,", 1.25, 1.50, 0.9, null),
        new("vamos", 1.80, 2.10, 0.9, null), new("falar", 2.15, 2.40, 0.9, null), new("da", 2.45, 2.55, 0.9, null),
        new("esteira", 2.60, 3.00, 0.9, null), new("hoje", 3.05, 3.30, 0.9, null), new("pessoal.", 3.35, 3.80, 0.9, null),
        new("é…", 4.60, 4.90, 0.9, null),
        new("Isso", 5.70, 5.90, 0.9, null), new("é", 5.95, 6.05, 0.9, null), new("simples.", 6.10, 6.60, 0.9, null),
    ];

    [Fact]
    public void Detects_repeated_take_and_isolated_hesitation_and_keeps_the_second_take()
    {
        var cuts = SpeechErrorDetector.DetectCuts(WithErrors());

        Assert.Equal(
            [(new TimeRange(0.00, 1.72), "repetição"), (new TimeRange(4.60, 4.90), "hesitação")],
            cuts);

        var keep = SpeechErrorDetector.Subtract([new TimeRange(0, 6.6)], cuts.Select(c => c.Range).ToList());
        Assert.Equal([new TimeRange(1.72, 4.60), new TimeRange(4.90, 6.6)], keep);
    }

    [Fact]
    public void Retake_cut_ends_one_lead_before_the_second_take_using_the_edl_default()
    {
        var words = WithErrors();
        var secondTakeStart = words[5].Start; // "vamos" do recomeço, em 1.80 s

        var cut = Assert.Single(SpeechErrorDetector.DetectCuts(words), c => c.Kind == "repetição");

        Assert.Equal(secondTakeStart - new EdlParams().LeadSeconds, cut.Range.Out, 3);
        Assert.True(cut.Range.Out > words[4].End, "o corte deve terminar depois da última palavra da tomada errada");
    }

    [Fact]
    public void Intentional_repetition_without_pause_is_not_cut()
    {
        // "não é não é não é" dito de uma vez, sem pausa (gap de 0.05 s) antes da segunda ocorrência do n-grama.
        IReadOnlyList<TranscriptWord> words =
        [
            new("isso", 0.00, 0.20, 1, null), new("aqui", 0.25, 0.45, 1, null), new("é", 0.50, 0.60, 1, null), new("ouro", 0.65, 0.95, 1, null),
            new("isso", 1.00, 1.20, 1, null), new("aqui", 1.25, 1.45, 1, null), new("é", 1.50, 1.60, 1, null), new("ouro", 1.65, 1.95, 1, null),
        ];

        Assert.Empty(SpeechErrorDetector.DetectCuts(words));
    }

    [Fact]
    public void Same_repetition_after_a_pause_is_a_retake()
    {
        // Mesmas palavras do teste acima, mas com pausa de 0.25 s antes da segunda ocorrência.
        IReadOnlyList<TranscriptWord> words =
        [
            new("isso", 0.00, 0.20, 1, null), new("aqui", 0.25, 0.45, 1, null), new("é", 0.50, 0.60, 1, null), new("ouro", 0.65, 0.95, 1, null),
            new("isso", 1.20, 1.40, 1, null), new("aqui", 1.45, 1.65, 1, null), new("é", 1.70, 1.80, 1, null), new("ouro", 1.85, 2.15, 1, null),
        ];

        var cut = Assert.Single(SpeechErrorDetector.DetectCuts(words));
        Assert.Equal((new TimeRange(0.00, 1.12), "repetição"), cut);
    }

    [Fact]
    public void Ignores_repetition_more_than_10s_apart_and_hesitation_word_without_pauses()
    {
        IReadOnlyList<TranscriptWord> words =
        [
            new("a", 0.0, 0.2, 1, null), new("b", 0.3, 0.5, 1, null), new("c", 0.6, 0.8, 1, null), new("d", 0.9, 1.1, 1, null),
            new("tipo", 1.2, 1.4, 1, null),
            new("a", 12.0, 12.2, 1, null), new("b", 12.3, 12.5, 1, null), new("c", 12.6, 12.8, 1, null), new("d", 12.9, 13.1, 1, null),
        ];

        Assert.Empty(SpeechErrorDetector.DetectCuts(words));
    }

    [Fact]
    public void Distinct_sentences_that_start_with_the_same_ngram_are_not_a_retake()
    {
        // "O que a gente faz aqui é X." [pausa 0,3 s] "O que a gente quer é Y." — mesmo 4-grama, frases diferentes: nada a cortar.
        IReadOnlyList<TranscriptWord> words =
        [
            new("O", 0.00, 0.10, 1, null), new("que", 0.15, 0.30, 1, null), new("a", 0.35, 0.40, 1, null), new("gente", 0.45, 0.70, 1, null),
            new("faz", 0.75, 0.90, 1, null), new("aqui", 0.95, 1.10, 1, null), new("é", 1.15, 1.20, 1, null), new("X.", 1.25, 1.50, 1, null),
            new("O", 1.80, 1.90, 1, null), new("que", 1.95, 2.10, 1, null), new("a", 2.15, 2.20, 1, null), new("gente", 2.25, 2.50, 1, null),
            new("quer", 2.55, 2.80, 1, null), new("é", 2.85, 2.90, 1, null), new("Y.", 2.95, 3.20, 1, null),
        ];

        Assert.Empty(SpeechErrorDetector.DetectCuts(words));
    }

    [Fact]
    public void Three_takes_cut_the_first_two_and_keep_the_last()
    {
        IReadOnlyList<TranscriptWord> words =
        [
            new("isso", 0.00, 0.20, 1, null), new("aqui", 0.25, 0.45, 1, null), new("é", 0.50, 0.60, 1, null), new("ouro", 0.65, 0.95, 1, null),
            new("isso", 1.20, 1.40, 1, null), new("aqui", 1.45, 1.65, 1, null), new("é", 1.70, 1.80, 1, null), new("ouro", 1.85, 2.15, 1, null),
            new("isso", 2.40, 2.60, 1, null), new("aqui", 2.65, 2.85, 1, null), new("é", 2.90, 3.00, 1, null), new("ouro", 3.05, 3.35, 1, null),
        ];

        var cuts = SpeechErrorDetector.DetectCuts(words);

        Assert.Equal([(new TimeRange(0.00, 1.12), "repetição"), (new TimeRange(1.20, 2.32), "repetição")], cuts);
        Assert.Equal([new TimeRange(2.32, 3.35)], SpeechErrorDetector.Subtract([new TimeRange(0, 3.35)], cuts.Select(c => c.Range).ToList()));
    }

    [Fact]
    public void Hesitation_inside_the_cut_take_is_not_reported_twice()
    {
        // Primeira tomada com "é…" isolado no meio, depois a tomada inteira de novo (também com o "é…").
        IReadOnlyList<TranscriptWord> words =
        [
            new("vamos", 0.00, 0.30, 1, null), new("é…", 1.00, 1.20, 1, null), new("falar", 2.00, 2.30, 1, null), new("da", 2.35, 2.45, 1, null), new("esteira", 2.50, 2.90, 1, null),
            new("vamos", 3.20, 3.50, 1, null), new("é…", 4.20, 4.40, 1, null), new("falar", 5.20, 5.50, 1, null), new("da", 5.55, 5.65, 1, null), new("esteira", 5.70, 6.10, 1, null),
        ];

        var cuts = SpeechErrorDetector.DetectCuts(words);

        Assert.Equal([(new TimeRange(0.00, 3.12), "repetição"), (new TimeRange(4.20, 4.40), "hesitação")], cuts);
    }

    [Fact]
    public void Subtract_drops_pieces_shorter_than_min_clip()
    {
        var keep = SpeechErrorDetector.Subtract([new TimeRange(0, 5)], [new TimeRange(0.1, 2), new TimeRange(1.5, 4.9)]);
        Assert.Empty(keep);
    }
}
