namespace Esteira.Domain.Enums;

/// <summary>Etapas da esteira. A ordem numérica é a ordem de execução.</summary>
public enum VideoStage
{
    Uploaded = 0,
    Ingested = 10,
    Transcribed = 20,
    Planned = 30,
    Rendered = 40,
    CopyReady = 50,
    AwaitingApproval = 60,
    Approved = 70,
    Published = 80,
    Failed = 999,
}
