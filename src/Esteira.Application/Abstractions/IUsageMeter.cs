using Esteira.Domain.Entities;

namespace Esteira.Application.Abstractions;

/// <summary>Registra consumo no job. Toda etapa que custa dinheiro passa por aqui.</summary>
public interface IUsageMeter
{
    void RecordLlmTokens(VideoJob job, int tokens, string purpose);
    void RecordRenderMinutes(VideoJob job, double minutes, string purpose);
    void RecordTranscribedMinutes(VideoJob job, double minutes, string purpose);
}
