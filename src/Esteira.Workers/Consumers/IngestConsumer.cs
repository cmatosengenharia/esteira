using Esteira.Application.Abstractions;
using Esteira.Application.Contracts;
using Esteira.Application.Messaging;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;

namespace Esteira.Workers.Consumers;

/// <summary>Fila `ingest`: ffprobe (duração, fps) e extração de áudio WAV mono 16 kHz.</summary>
public sealed class IngestConsumer(
    IEsteiraDbContext db, ITenantContextSetter tenantSetter, IClock clock, ILogger<IngestConsumer> logger,
    IObjectStorage storage, IMediaInspector inspector, IAudioExtractor audioExtractor)
    : StageConsumer<IngestRequested>(db, tenantSetter, clock, logger)
{
    protected override VideoStage TargetStage => VideoStage.Ingested;

    protected override async Task<object?> ExecuteAsync(VideoJob job, CancellationToken ct)
    {
        var sourcePath = await storage.GetLocalPathAsync(job.SourceObjectKey, ct);
        var info = await inspector.InspectAsync(sourcePath, ct);
        if (!info.HasAudio) throw new Domain.Exceptions.DomainException("O vídeo não tem trilha de áudio");

        job.DurationSeconds = info.DurationSeconds;
        job.Fps = info.Fps;

        var audioKey = ObjectKeys.Audio(job);
        await audioExtractor.ExtractWavAsync(sourcePath, storage.GetLocalPathForWrite(audioKey), ct);
        job.AudioObjectKey = audioKey;

        return new TranscribeRequested(job.Id, job.TenantId);
    }
}
