using Esteira.Application.Abstractions;
using Esteira.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Esteira.Infrastructure.Metering;

/// <summary>Acumula no VideoJob.Usage (persistido pelo worker) e loga cada registro para auditoria de custo.</summary>
public sealed class UsageMeter(ILogger<UsageMeter> logger) : IUsageMeter
{
    public void RecordLlmTokens(VideoJob job, int tokens, string purpose)
    {
        job.Usage.LlmTokens += tokens;
        logger.LogInformation("usage job={JobId} llm_tokens+={Tokens} total={Total} purpose={Purpose}", job.Id, tokens, job.Usage.LlmTokens, purpose);
    }

    public void RecordRenderMinutes(VideoJob job, double minutes, string purpose)
    {
        job.Usage.RenderMinutes += minutes;
        logger.LogInformation("usage job={JobId} render_minutes+={Minutes:0.###} total={Total:0.###} purpose={Purpose}", job.Id, minutes, job.Usage.RenderMinutes, purpose);
    }

    public void RecordTranscribedMinutes(VideoJob job, double minutes, string purpose)
    {
        job.Usage.TranscribedMinutes += minutes;
        logger.LogInformation("usage job={JobId} transcribed_minutes+={Minutes:0.###} total={Total:0.###} purpose={Purpose}", job.Id, minutes, job.Usage.TranscribedMinutes, purpose);
    }
}
