namespace Esteira.Application.Messaging;

/// <summary>Uma mensagem por fila. Carregam só ids: o worker recarrega o job já filtrado pelo tenant.</summary>
public interface IJobStageMessage
{
    Guid JobId { get; }
    Guid TenantId { get; }
}

public sealed record IngestRequested(Guid JobId, Guid TenantId) : IJobStageMessage;
public sealed record TranscribeRequested(Guid JobId, Guid TenantId) : IJobStageMessage;
public sealed record PlanRequested(Guid JobId, Guid TenantId) : IJobStageMessage;
public sealed record RenderRequested(Guid JobId, Guid TenantId) : IJobStageMessage;

public static class Queues
{
    public const string Ingest = "ingest";
    public const string Transcribe = "transcribe";
    public const string Plan = "plan";
    public const string Render = "render";
}
