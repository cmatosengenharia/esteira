using Esteira.Infrastructure.Media.Audio;

namespace Esteira.UnitTests.Media;

/// <summary>Gera WAV mono 16 kHz com trechos de "fala" (seno a 0.3 de amplitude) e silêncio (zero).</summary>
public static class SyntheticAudio
{
    public const int SampleRate = 16000;

    public static WavFile Build(params (bool Speech, double Seconds)[] segments)
    {
        var total = (int)Math.Round(segments.Sum(s => s.Seconds) * SampleRate);
        var samples = new short[total];
        var pos = 0;
        foreach (var (speech, seconds) in segments)
        {
            var n = (int)Math.Round(seconds * SampleRate);
            for (var i = 0; i < n && pos < total; i++, pos++)
                samples[pos] = speech ? (short)(Math.Sin(2 * Math.PI * 220 * pos / SampleRate) * 0.3 * short.MaxValue) : (short)0;
        }
        return new WavFile(SampleRate, samples);
    }
}
