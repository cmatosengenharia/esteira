using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Audio;
using Esteira.Infrastructure.Media.Planning;

namespace Esteira.UnitTests.Media;

public class ShortsPlannerTests
{
    /// <summary>Fala sintética: frases de ~8 palavras a 2,5 palavras/s, pausa de 0,7 s entre frases, até `seconds`.</summary>
    public static IReadOnlyList<TranscriptWord> Speech(double seconds, string? hookAt30 = null)
    {
        var words = new List<TranscriptWord>();
        double t = 0; var n = 0;
        while (t < seconds)
        {
            var sentence = hookAt30 is not null && Math.Abs(t - 30) < 2.5 ? hookAt30 : $"frase {n} sobre a esteira de vídeo com legenda pronta.";
            var parts = sentence.Split(' ');
            for (var i = 0; i < parts.Length; i++)
            {
                words.Add(new TranscriptWord(parts[i], Math.Round(t, 3), Math.Round(t + 0.3, 3), 0.9, null));
                t += 0.4;
            }
            t += 0.7; n++;
        }
        return words;
    }

    private static readonly EdlParams P = new();

    [Fact]
    public void Three_minute_talk_yields_three_non_overlapping_clips_of_30_to_60_seconds_on_sentence_boundaries()
    {
        var words = Speech(180);
        var keep = new[] { new TimeRange(0, 180) };

        var clips = ShortsPlanner.Plan(words, keep, P, new ShortsOptions());

        Assert.Equal(3, clips.Count);
        var sentences = ShortsPlanner.Sentences(words, keep, P.SplitGapSeconds);
        for (var i = 0; i < clips.Count; i++)
        {
            var c = clips[i];
            var kept = ShortsPlanner.KeptDuration(keep, c.In, c.Out);
            Assert.True(kept is >= 30 and <= 61, $"clip {i} com {kept:0.0}s");
            Assert.Contains(sentences, s => Math.Abs(Math.Max(0, s.Start - P.LeadSeconds) - c.In) < 0.001);
            Assert.Contains(sentences, s => Math.Abs(s.End + P.TrailSeconds - c.Out) < 0.001);
            if (i > 0) Assert.True(c.In >= clips[i - 1].Out, "clipes não podem se sobrepor");
            Assert.False(string.IsNullOrWhiteSpace(c.Title));
            Assert.InRange(c.Score, 0, 100);
        }
    }

    [Fact]
    public void Short_video_yields_no_clips()
    {
        var words = Speech(20);
        Assert.Empty(ShortsPlanner.Plan(words, [new TimeRange(0, 20)], P, new ShortsOptions()));
    }

    [Fact]
    public void Duration_counts_only_kept_time_and_a_cut_ends_a_sentence()
    {
        var words = Speech(120);
        // corta 40–80: o que sobra são 80 s mantidos, em dois trechos
        var keep = new[] { new TimeRange(0, 40), new TimeRange(80, 120) };

        var clips = ShortsPlanner.Plan(words, keep, P, new ShortsOptions(MaxClips: 3));

        Assert.NotEmpty(clips);
        Assert.All(clips, c => Assert.InRange(ShortsPlanner.KeptDuration(keep, c.In, c.Out), 30, 61));
        var sentences = ShortsPlanner.Sentences(words, keep, P.SplitGapSeconds);
        Assert.DoesNotContain(sentences, s => s.Start < 40 && s.End > 80); // nenhuma frase atravessa o corte
        Assert.Equal(40, ShortsPlanner.KeptDuration(keep, 20, 100));
    }

    [Fact]
    public void Question_with_a_number_at_the_start_scores_higher_than_a_plain_sentence()
    {
        var plain = ShortsPlanner.Plan(Speech(180), [new TimeRange(0, 180)], P, new ShortsOptions(MaxClips: 10));
        var hooked = ShortsPlanner.Plan(Speech(180, "Por que 3 erros de fala somem em segundos?"), [new TimeRange(0, 180)], P, new ShortsOptions(MaxClips: 10));

        var hook = Assert.Single(hooked, c => c.HookSentence!.StartsWith("Por que 3"));
        Assert.True(hook.Score > plain.Max(c => c.Score), $"gancho {hook.Score} deveria superar {plain.Max(c => c.Score)}");
        Assert.Equal(hooked.OrderByDescending(c => c.Score).First().Title, hook.Title);
    }

    [Fact]
    public void ClipKeep_intersects_keep_with_the_clip_window()
    {
        var keep = new[] { new TimeRange(0, 10), new TimeRange(12, 20), new TimeRange(25, 30) };
        var inside = ShortsPlanner.ClipKeep(keep, new TimeRange(8, 26));
        Assert.Equal([new TimeRange(8, 10), new TimeRange(12, 20), new TimeRange(25, 26)], inside);
        Assert.Empty(ShortsPlanner.ClipKeep(keep, new TimeRange(10, 12)));
    }
}
