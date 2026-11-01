namespace Esteira.Application.Abstractions;

public sealed record MediaInfo(double DurationSeconds, double Fps, int Width, int Height, bool HasAudio);

/// <summary>ffprobe: duração, fps e dimensões.</summary>
public interface IMediaInspector
{
    Task<MediaInfo> InspectAsync(string localPath, CancellationToken cancellationToken);
}

/// <summary>ffmpeg: extrai áudio WAV mono 16 kHz (formato que o whisper.cpp espera).</summary>
public interface IAudioExtractor
{
    Task ExtractWavAsync(string videoLocalPath, string wavOutputPath, CancellationToken cancellationToken);
}
