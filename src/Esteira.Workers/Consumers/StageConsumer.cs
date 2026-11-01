using Esteira.Application.Abstractions;
using Esteira.Application.Messaging;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using Esteira.Domain.Exceptions;
using Esteira.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Esteira.Workers.Consumers;

/// <summary>
/// Esqueleto de todo worker de etapa:
/// 1. fixa o tenant da mensagem (toda query abaixo já sai filtrada);
/// 2. recarrega o job; se não existe (outro tenant) ou já passou da etapa, ignora (idempotência);
/// 3. checa orçamento, abre timing, executa, fecha timing, salva e publica a próxima mensagem;
/// 4. erro de domínio/orçamento: job vai para Failed sem retentativa;
///    erro de infraestrutura: retentativa com backoff; esgotada, Failed + dead-letter.
/// </summary>
public abstract class StageConsumer<TMessage>(
    IEsteiraDbContext db,
    ITenantContextSetter tenantSetter,
    IClock clock,
    ILogger logger) : IConsumer<TMessage>
    where TMessage : class, IJobStageMessage
{
    protected IEsteiraDbContext Db { get; } = db;
    protected IClock Clock { get; } = clock;
    protected ILogger Logger { get; } = logger;

    protected abstract VideoStage TargetStage { get; }

    /// <summary>Executa a etapa e devolve a mensagem da próxima (ou null no fim da esteira).</summary>
    protected abstract Task<object?> ExecuteAsync(VideoJob job, CancellationToken cancellationToken);

    /// <summary>Checagem de orçamento antes de gastar. Lança <see cref="BudgetExceededException"/>.</summary>
    protected virtual void EnsureBudget(VideoJob job) { }

    public async Task Consume(ConsumeContext<TMessage> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;
        tenantSetter.SetTenant(msg.TenantId);

        var job = await Db.VideoJobs.SingleOrDefaultAsync(j => j.Id == msg.JobId, ct);
        if (job is null)
        {
            Logger.LogWarning("job {JobId} não encontrado para tenant {TenantId}; mensagem ignorada", msg.JobId, msg.TenantId);
            return;
        }
        if (job.Stage == VideoStage.Failed || job.AlreadyPast(TargetStage))
        {
            Logger.LogInformation("job {JobId} em {Stage}; etapa {Target} ignorada (idempotência)", job.Id, job.Stage, TargetStage);
            return;
        }

        var attempt = context.GetRetryAttempt();
        try
        {
            EnsureBudget(job);
            job.BeginStage(TargetStage, Clock.UtcNow);
            await Db.SaveChangesAsync(ct);

            Logger.LogInformation("job {JobId} etapa {Target} iniciada (tentativa {Attempt})", job.Id, TargetStage, attempt);
            var next = await ExecuteAsync(job, ct);

            job.CompleteStage(TargetStage, Clock.UtcNow);
            await Db.SaveChangesAsync(ct);
            Logger.LogInformation("job {JobId} etapa {Target} concluída em {Ms} ms", job.Id, TargetStage,
                job.Timings.Last(t => t.Stage == TargetStage).DurationMs);

            if (next is not null) await context.Publish(next, ct);
        }
        catch (BudgetExceededException ex)
        {
            await FailAsync(job, $"BudgetExceeded: {ex.Message}", ct);
        }
        catch (DomainException ex)
        {
            await FailAsync(job, ex.Message, ct);
        }
        catch (Exception ex) when (attempt < MessagingExtensions.MaxRetries)
        {
            Logger.LogWarning(ex, "job {JobId} etapa {Target} falhou na tentativa {Attempt}; vai retentar", job.Id, TargetStage, attempt);
            throw;
        }
        catch (Exception ex)
        {
            await FailAsync(job, $"{ex.GetType().Name}: {ex.Message}", ct);
            throw; // esgotou retentativas: mensagem segue para a fila de erro (dead-letter)
        }
    }

    private async Task FailAsync(VideoJob job, string reason, CancellationToken ct)
    {
        Logger.LogError("job {JobId} etapa {Target} FALHOU: {Reason}", job.Id, TargetStage, reason);
        job.Fail(reason, Clock.UtcNow);
        await Db.SaveChangesAsync(ct);
    }
}
