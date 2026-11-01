namespace Esteira.Application.Abstractions;

public sealed record TranscriptWord(string Word, double Start, double End, double Score, string? Speaker);

public sealed record Transcript(string Language, IReadOnlyList<TranscriptWord> Words)
{
    public string Text => string.Join(' ', Words.Select(w => w.Word));
    public double DurationSeconds => Words.Count == 0 ? 0 : Words[^1].End;
}

/// <summary>Áudio WAV mono 16 kHz → transcrição com timestamp por palavra.</summary>
public interface ITranscriber
{
    Task<Transcript> TranscribeAsync(string wavLocalPath, string? languageHint, CancellationToken cancellationToken);
}
