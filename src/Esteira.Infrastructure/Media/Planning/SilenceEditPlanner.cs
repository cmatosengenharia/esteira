using Esteira.Application.Abstractions;
using Esteira.Domain.Enums;
using Esteira.Domain.Exceptions;
using Esteira.Infrastructure.Media.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esteira.Infrastructure.Media.Planning;

/// <summary>
/// Planner da Fase 0 (sem LLM): corta silêncios pelo WAV e adiciona uma camada de legendas por frase.
/// Lê os parâmetros padrão do contrato; a style bible (quando houver) só ajusta a saída. O planner com Claude entra na Fase 1.
/// Custo: 0 tokens; CPU proporcional à duração do áudio.
/// </summary>
public sealed class SilenceEditPlanner(ILogger<SilenceEditPlanner> logger, IOptions<MediaOptions>? options = null) : IEditPlanner
{
    private readonly MediaOptions _o = options?.Value ?? new MediaOptions();

    public Task<string> PlanAsync(PlanRequest request, CancellationToken cancellationToken)
    {
        var job = request.Job;
        var wav = WavFile.Read(request.WavLocalPath);
        var p = new EdlParams();
        var silenceKeep = SilenceDetector.DetectKeep(wav, new SilenceParams(
            ThresholdDb: p.SilenceThresholdDb, MinSilenceSeconds: p.MinSilenceSeconds,
            LeadSeconds: p.LeadSeconds, TrailSeconds: p.TrailSeconds));
        if (silenceKeep.Count == 0)
            throw new DomainException("Nenhum trecho com fala acima do limiar de silêncio; nada para manter");

        var words = request.Transcript.Words;
        var cuts = SpeechErrorDetector.DetectCuts(words);
        var keep = SpeechErrorDetector.Subtract(silenceKeep, cuts.Select(c => c.Range).ToList());
        if (keep.Count == 0)
            throw new DomainException("Nada sobrou depois de cortar repetições e hesitações");
        var annotations = keep.Select(r =>
        {
            var inside = words.Where(w => (w.Start + w.End) / 2 >= r.In && (w.Start + w.End) / 2 < r.Out).ToList();
            return (inside.FirstOrDefault()?.Word ?? "", inside.LastOrDefault()?.Word ?? "", "fala acima do limiar de silêncio");
        }).ToList();

        var totalKept = keep.Sum(r => r.Duration);
        var clips = ShortsPlanner.Plan(words, keep, p, new ShortsOptions(_o.ShortsPerVideo, _o.ShortMinSeconds, _o.ShortMaxSeconds, _o.ShortMinSourceSeconds));
        var doc = new EdlDocument
        {
            SourceObjectKey = job.SourceObjectKey,
            SourceDurationSeconds = job.DurationSeconds ?? wav.DurationSeconds,
            SourceFps = job.Fps ?? 30,
            StyleTemplateId = job.StyleTemplateId?.ToString(),
            Strategy = $"Fase 0: remove {silenceKeep.Count - 1} silêncio(s) >= {p.MinSilenceSeconds}s abaixo de {p.SilenceThresholdDb} dB, {cuts.Count(c => c.Kind == "repetição")} repetição(ões) e {cuts.Count(c => c.Kind == "hesitação")} hesitação(ões), legenda por frase e {clips.Count} short(s); {wav.DurationSeconds:0.0}s → {totalKept:0.0}s",
            Keep = keep,
            KeepAnnotations = annotations,
            Captions = new CaptionStyle(),
            Params = p,
            Outputs = [(AspectRatio.Vertical9x16, "center")],
            TargetDurationSeconds = Math.Round(totalKept, 3),
            Clips = clips,
        };

        var json = doc.ToJson();
        EdlValidator.EnsureValid(json);
        logger.LogInformation("plan job={JobId} keep={Keep} trechos, {Kept:0.0}s de {Total:0.0}s, {Clips} short(s)", job.Id, keep.Count, totalKept, wav.DurationSeconds, clips.Count);
        return Task.FromResult(json);
    }
}
