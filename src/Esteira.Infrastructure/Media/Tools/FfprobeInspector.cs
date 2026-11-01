using System.Globalization;
using System.Text.Json;
using Esteira.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Esteira.Infrastructure.Media.Tools;

public sealed class FfprobeInspector(ProcessRunner runner, IOptions<MediaOptions> options) : IMediaInspector
{
    public async Task<MediaInfo> InspectAsync(string localPath, CancellationToken ct)
    {
        var result = await runner.RunAsync(options.Value.FfprobePath,
            ["-v", "error", "-print_format", "json", "-show_streams", "-show_format", localPath], ct);
        if (!result.Succeeded) throw new InvalidOperationException($"ffprobe falhou ({result.ExitCode}): {ProcessRunner.Tail(result.StdErr)}");

        using var doc = JsonDocument.Parse(result.StdOut);
        var root = doc.RootElement;
        double duration = 0;
        if (root.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var d))
            duration = ParseDouble(d.GetString());

        double fps = 0; int width = 0, height = 0; bool hasAudio = false;
        foreach (var stream in root.GetProperty("streams").EnumerateArray())
        {
            var type = stream.GetProperty("codec_type").GetString();
            if (type == "video" && width == 0)
            {
                width = stream.GetProperty("width").GetInt32();
                height = stream.GetProperty("height").GetInt32();
                fps = ParseRate(stream.TryGetProperty("avg_frame_rate", out var afr) ? afr.GetString() : null);
                if (fps <= 0) fps = ParseRate(stream.TryGetProperty("r_frame_rate", out var rfr) ? rfr.GetString() : null);
                if (duration <= 0 && stream.TryGetProperty("duration", out var sd)) duration = ParseDouble(sd.GetString());
            }
            else if (type == "audio") hasAudio = true;
        }
        if (width == 0) throw new InvalidOperationException("ffprobe não encontrou trilha de vídeo");
        return new MediaInfo(duration, fps, width, height, hasAudio);
    }

    private static double ParseDouble(string? s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>"30000/1001" → 29.97</summary>
    public static double ParseRate(string? rate)
    {
        if (string.IsNullOrWhiteSpace(rate)) return 0;
        var parts = rate.Split('/');
        var num = ParseDouble(parts[0]);
        var den = parts.Length > 1 ? ParseDouble(parts[1]) : 1;
        return den == 0 ? 0 : num / den;
    }
}
