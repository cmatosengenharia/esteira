using System.Text.Json;
using Esteira.Application.Abstractions;
using Esteira.Application.Contracts;
using Esteira.Application.Messaging;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;

namespace Esteira.Workers.Consumers;

/// <summary>Fila `transcribe`: whisper.cpp local → Transcript com timestamps por palavra. Registra minutos transcritos.</summary>
public sealed class TranscribeConsumer(
    IEsteiraDbContext db, ITenantContextSetter tenantSetter, IClock clock, ILogger<TranscribeConsumer> logger,
    IObjectStorage storage, ITranscriber transcriber, IUsageMeter usage)
    : StageConsumer<TranscribeRequested>(db, tenantSetter, clock, logger)
{
    protected override VideoStage TargetStage => VideoStage.Transcribed;

    protected override async Task<object?> ExecuteAsync(VideoJob job, CancellationToken ct)
    {
        var audioKey = job.AudioObjectKey ?? throw new Domain.Exceptions.DomainException("Job sem áudio extraído");
        var wavPath = await storage.GetLocalPathAsync(audioKey, ct);
        var transcript = await transcriber.TranscribeAsync(wavPath, languageHint: null, ct);

        job.TranscriptJson = JsonSerializer.Serialize(transcript, JsonDefaults.Options);
        await using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(job.TranscriptJson)))
            await storage.PutAsync(ObjectKeys.Transcript(job), ms, ct);
        usage.RecordTranscribedMinutes(job, (job.DurationSeconds ?? transcript.DurationSeconds) / 60.0, "whisper.cpp small");

        return new PlanRequested(job.Id, job.TenantId);
    }
}
