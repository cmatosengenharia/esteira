using Esteira.Infrastructure.Media.Audio;

namespace Esteira.UnitTests.Media;

public class SilenceDetectorTests
{
    private static readonly SilenceParams P = new();

    [Fact]
    public void Wav_round_trips_through_writer_and_parser()
    {
        var wav = SyntheticAudio.Build((true, 0.5), (false, 0.25));
        var parsed = WavFile.Parse(WavFile.Write(wav.SampleRate, wav.Samples));
        Assert.Equal(wav.SampleRate, parsed.SampleRate);
        Assert.Equal(wav.Samples, parsed.Samples);
        Assert.Equal(0.75, parsed.DurationSeconds, 3);
    }

    [Fact]
    public void Levels_distinguish_speech_from_silence()
    {
        var wav = SyntheticAudio.Build((true, 1), (false, 1));
        var levels = SilenceDetector.Levels(wav.Samples, wav.SampleRate, P.WindowSeconds);
        Assert.True(levels[0] > -20, $"fala deveria estar acima de -20 dB, veio {levels[0]}");
        Assert.True(levels[^1] <= -90, $"silêncio deveria estar no piso, veio {levels[^1]}");
    }

    [Fact]
    public void Long_silence_is_cut_short_silence_is_kept_and_margins_apply()
    {
        // fala 1s | silêncio 1s (corta) | fala 0.3s | silêncio 0.2s (não corta) | fala 1s | silêncio 2s (corta)
        var wav = SyntheticAudio.Build((true, 1), (false, 1), (true, 0.3), (false, 0.2), (true, 1), (false, 2));
        var keep = SilenceDetector.DetectKeep(wav, P);

        Assert.Equal(2, keep.Count);
        // janelas de 1/30 s quantizam as bordas; margem de 0.08 s antes e 0.12 s depois
        Assert.Equal(0, keep[0].In, 2);
        Assert.InRange(keep[0].Out, 1 + P.TrailSeconds - 0.01, 1 + P.TrailSeconds + 0.04);
        Assert.InRange(keep[1].In, 2 - P.LeadSeconds - 0.04, 2 - P.LeadSeconds + 0.01);
        Assert.InRange(keep[1].Out, 3.5 + P.TrailSeconds - 0.01, 3.5 + P.TrailSeconds + 0.04);
        Assert.Equal(5.5, wav.DurationSeconds, 3);
    }

    [Fact]
    public void Tiny_blips_are_discarded_and_pure_silence_keeps_nothing()
    {
        var blip = SyntheticAudio.Build((false, 1), (true, 0.05), (false, 1));
        Assert.Empty(SilenceDetector.DetectKeep(blip, P));
        Assert.Empty(SilenceDetector.DetectKeep(SyntheticAudio.Build((false, 2)), P));
    }

    [Fact]
    public void Lower_threshold_keeps_quieter_audio()
    {
        var wav = SyntheticAudio.Build((true, 1));
        for (var i = 0; i < wav.Samples.Length; i++) wav.Samples[i] = (short)(wav.Samples[i] / 40); // ~-43 dBFS
        Assert.Empty(SilenceDetector.DetectKeep(wav, P));
        Assert.Single(SilenceDetector.DetectKeep(wav, P with { ThresholdDb = -60 }));
    }
}
