using Esteira.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Esteira.Infrastructure.Media.Tools;

/// <summary>WAV PCM 16-bit, mono, 16 kHz: o formato de entrada do whisper.cpp e do detector de silêncio.</summary>
public sealed class FfmpegAudioExtractor(ProcessRunner runner, IOptions<MediaOptions> options) : IAudioExtractor
{
    public async Task ExtractWavAsync(string videoLocalPath, string wavOutputPath, CancellationToken ct)
    {
        var result = await runner.RunAsync(options.Value.FfmpegPath,
            ["-y", "-v", "error", "-i", videoLocalPath, "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", wavOutputPath], ct);
        if (!result.Succeeded) throw new InvalidOperationException($"ffmpeg (extração de áudio) falhou ({result.ExitCode}): {ProcessRunner.Tail(result.StdErr)}");
    }
}
