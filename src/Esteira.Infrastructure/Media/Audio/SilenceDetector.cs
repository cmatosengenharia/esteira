namespace Esteira.Infrastructure.Media.Audio;

public sealed record TimeRange(double In, double Out)
{
    public double Duration => Out - In;
}

/// <summary>Parâmetros do detector. Os padrões batem com `params` de docs/contracts/edl.schema.json.</summary>
public sealed record SilenceParams(
    double ThresholdDb = -35,
    double MinSilenceSeconds = 0.5,
    double LeadSeconds = 0.08,
    double TrailSeconds = 0.12,
    double MinClipSeconds = 0.15,
    double WindowSeconds = 1.0 / 30);

/// <summary>
/// Detecção de silêncio portada do auto-editor (WyattBlue/auto-editor, Unlicense):
/// nível por janela → limiar → margem antes/depois → descarte de clipes e cortes curtos demais.
/// Diferença deliberada: nível em RMS dBFS (limiar em dB) em vez de amplitude máxima normalizada 0..1.
/// Registro em THIRD_PARTY_NOTICES.md.
/// </summary>
public static class SilenceDetector
{
    /// <summary>RMS em dBFS por janela de <paramref name="windowSeconds"/>. Janela sem sinal = -100 dB.</summary>
    public static double[] Levels(ReadOnlySpan<short> samples, int sampleRate, double windowSeconds)
    {
        var window = Math.Max(1, (int)Math.Round(sampleRate * windowSeconds));
        var count = (samples.Length + window - 1) / window;
        var levels = new double[count];
        for (var i = 0; i < count; i++)
        {
            var start = i * window;
            var len = Math.Min(window, samples.Length - start);
            double acc = 0;
            for (var j = 0; j < len; j++)
            {
                double v = samples[start + j] / 32768.0;
                acc += v * v;
            }
            var rms = Math.Sqrt(acc / len);
            levels[i] = rms <= 1e-9 ? -100 : 20 * Math.Log10(rms);
        }
        return levels;
    }

    /// <summary>Trechos com fala (a manter), em ordem, meio-abertos [in, out), recortados em [0, duration].</summary>
    public static IReadOnlyList<TimeRange> DetectKeep(WavFile wav, SilenceParams p)
        => DetectKeep(Levels(wav.Samples, wav.SampleRate, p.WindowSeconds), p.WindowSeconds, wav.DurationSeconds, p);

    public static IReadOnlyList<TimeRange> DetectKeep(double[] levels, double windowSeconds, double duration, SilenceParams p)
    {
        // 1. janelas acima do limiar viram runs contíguos
        var loud = new List<TimeRange>();
        var i = 0;
        while (i < levels.Length)
        {
            if (levels[i] <= p.ThresholdDb) { i++; continue; }
            var start = i;
            while (i < levels.Length && levels[i] > p.ThresholdDb) i++;
            loud.Add(new TimeRange(start * windowSeconds, Math.Min(duration, i * windowSeconds)));
        }
        if (loud.Count == 0) return [];

        // 2. silêncio menor que MinSilence não corta: funde os runs vizinhos
        var merged = new List<TimeRange> { loud[0] };
        foreach (var r in loud.Skip(1))
        {
            var last = merged[^1];
            if (r.In - last.Out < p.MinSilenceSeconds) merged[^1] = new TimeRange(last.In, r.Out);
            else merged.Add(r);
        }

        // 3. clipes curtos demais (ruído, clique) são descartados
        var kept = merged.Where(r => r.Duration >= p.MinClipSeconds).ToList();
        if (kept.Count == 0) return [];

        // 4. margem antes/depois e nova fusão de sobreposições
        var result = new List<TimeRange>();
        foreach (var r in kept)
        {
            var padded = new TimeRange(Math.Max(0, r.In - p.LeadSeconds), Math.Min(duration, r.Out + p.TrailSeconds));
            if (result.Count > 0 && padded.In <= result[^1].Out)
                result[^1] = new TimeRange(result[^1].In, Math.Max(result[^1].Out, padded.Out));
            else
                result.Add(padded);
        }
        return result.Select(r => new TimeRange(Math.Round(r.In, 3), Math.Round(r.Out, 3))).ToList();
    }
}
