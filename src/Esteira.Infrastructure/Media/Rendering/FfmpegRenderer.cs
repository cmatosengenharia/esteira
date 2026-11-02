using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Esteira.Application.Abstractions;
using Esteira.Application.Contracts;
using Esteira.Domain.Enums;
using Esteira.Domain.ValueObjects;
using Esteira.Infrastructure.Media.Audio;
using Esteira.Infrastructure.Media.Captions;
using Esteira.Infrastructure.Media.Planning;
using Esteira.Infrastructure.Media.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esteira.Infrastructure.Media.Rendering;

/// <summary>
/// Render da Fase 0 com ffmpeg: select/aselect dos trechos mantidos (concat sem re-decodificar duas vezes),
/// crop central para a proporção de cada saída, scale para 1080p e burn-in das legendas ASS.
/// Custo: minutos de parede do ffmpeg, registrados no IUsageMeter pelo worker. Hyperframes entra para composições HTML.
/// </summary>
public sealed class FfmpegRenderer(
    ProcessRunner runner,
    IObjectStorage storage,
    IMediaInspector inspector,
    IOptions<MediaOptions> options,
    ILogger<FfmpegRenderer> logger) : IRenderer
{
    private readonly MediaOptions _o = options.Value;

    public async Task<RenderResult> RenderAsync(RenderRequest request, CancellationToken ct)
    {
        var job = request.Job;
        EdlValidator.EnsureValid(request.EdlJson);
        var edl = EdlDocument.Parse(request.EdlJson);
        if (edl.Keep.Count == 0) throw new Domain.Exceptions.DomainException("EDL sem trechos a manter");

        var workDir = Path.GetFullPath(Path.Combine(_o.WorkRoot, job.Id.ToString("N")));
        Directory.CreateDirectory(workDir);

        var outputs = new List<JobOutput>();
        var totalRender = TimeSpan.Zero;
        var selectExpr = BuildSelect(edl.Keep);

        foreach (var (aspect, reframe) in edl.Outputs)
        {
            var (w, h) = aspect.ToPixels();
            string? assFile = null;
            if (edl.Captions is { } style && !string.IsNullOrEmpty(job.TranscriptJson))
            {
                var transcript = JsonSerializer.Deserialize<Transcript>(job.TranscriptJson, JsonDefaults.Options);
                if (transcript is { Words.Count: > 0 })
                {
                    var remapped = TimelineMapper.Remap(transcript.Words, edl.Keep);
                    var phrases = CaptionGrouper.Group(remapped, style.CombineWithinMs, style.MaxCharsPerLine, edl.Params.SplitGapSeconds);
                    assFile = $"captions-{aspect.ToContract().Replace(':', 'x')}.ass";
                    await File.WriteAllTextAsync(Path.Combine(workDir, assFile), AssWriter.Write(phrases, style, w, h), ct);
                }
            }

            if (reframe != "center")
                logger.LogInformation("reframe '{Reframe}' ainda não implementado; usando crop central", reframe);

            var key = ObjectKeys.Master(job, aspect);
            var outPath = storage.GetLocalPathForWrite(key);
            var filter = BuildFilter(selectExpr, w, h, assFile);
            var args = new List<string>
            {
                "-y", "-v", "error", "-i", request.SourceLocalPath,
                "-filter_complex", filter, "-map", "[v]", "-map", "[a]",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", outPath,
            };
            var sw = Stopwatch.StartNew();
            var result = await runner.RunAsync(_o.FfmpegPath, args, ct, workingDirectory: workDir);
            sw.Stop();
            if (!result.Succeeded) throw new InvalidOperationException($"ffmpeg (render {aspect.ToContract()}) falhou ({result.ExitCode}): {ProcessRunner.Tail(result.StdErr)}");
            totalRender += sw.Elapsed;

            var info = await inspector.InspectAsync(outPath, ct);
            outputs.Add(new JobOutput
            {
                Kind = OutputKind.Master,
                Aspect = aspect,
                ObjectKey = key,
                DurationSeconds = Math.Round(info.DurationSeconds, 3),
                SizeBytes = new FileInfo(outPath).Length,
            });
            logger.LogInformation("render job={JobId} {Aspect} {Dur:0.0}s em {Ms} ms → {Key}", job.Id, aspect.ToContract(), info.DurationSeconds, sw.ElapsedMilliseconds, key);
        }

        // Shorts: cada clipe é o mesmo select, restrito ao trecho do clipe, sempre 9:16 com legendas remapeadas.
        Transcript? transcriptForClips = null;
        if (edl.Clips.Count > 0 && edl.Captions is not null && !string.IsNullOrEmpty(job.TranscriptJson))
            transcriptForClips = JsonSerializer.Deserialize<Transcript>(job.TranscriptJson, JsonDefaults.Options);
        for (var n = 1; n <= edl.Clips.Count; n++)
        {
            var clip = edl.Clips[n - 1];
            var clipKeep = ShortsPlanner.ClipKeep(edl.Keep, new TimeRange(clip.In, clip.Out));
            if (clipKeep.Count == 0) { logger.LogWarning("clip {N} de job={JobId} fora dos trechos mantidos; ignorado", n, job.Id); continue; }
            var (w, h) = AspectRatio.Vertical9x16.ToPixels();
            string? assFile = null;
            if (transcriptForClips is { Words.Count: > 0 } && edl.Captions is { } style)
            {
                var phrases = CaptionGrouper.Group(TimelineMapper.Remap(transcriptForClips.Words, clipKeep), style.CombineWithinMs, style.MaxCharsPerLine, edl.Params.SplitGapSeconds);
                assFile = $"captions-clip-{n}.ass";
                await File.WriteAllTextAsync(Path.Combine(workDir, assFile), AssWriter.Write(phrases, style, w, h), ct);
            }
            var key = ObjectKeys.Clip(job, n);
            var outPath = storage.GetLocalPathForWrite(key);
            var args = new List<string>
            {
                "-y", "-v", "error", "-i", request.SourceLocalPath,
                "-filter_complex", BuildFilter(BuildSelect(clipKeep), w, h, assFile), "-map", "[v]", "-map", "[a]",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", outPath,
            };
            var sw = Stopwatch.StartNew();
            var result = await runner.RunAsync(_o.FfmpegPath, args, ct, workingDirectory: workDir);
            sw.Stop();
            if (!result.Succeeded) throw new InvalidOperationException($"ffmpeg (clip {n}) falhou ({result.ExitCode}): {ProcessRunner.Tail(result.StdErr)}");
            totalRender += sw.Elapsed;
            var info = await inspector.InspectAsync(outPath, ct);
            outputs.Add(new JobOutput
            {
                Kind = OutputKind.Clip, Aspect = AspectRatio.Vertical9x16, ObjectKey = key,
                DurationSeconds = Math.Round(info.DurationSeconds, 3), SizeBytes = new FileInfo(outPath).Length,
            });
            logger.LogInformation("clip {N} job={JobId} \"{Title}\" {Dur:0.0}s em {Ms} ms → {Key}", n, job.Id, clip.Title, info.DurationSeconds, sw.ElapsedMilliseconds, key);
        }

        return new RenderResult(outputs, Math.Round(totalRender.TotalMinutes, 4));
    }

    /// <summary>between(t,a,b)+between(t,c,d)…</summary>
    public static string BuildSelect(IReadOnlyList<TimeRange> keep)
        => string.Join('+', keep.Select(r => string.Create(CultureInfo.InvariantCulture, $"between(t,{r.In:0.###},{r.Out:0.###})")));

    public static string BuildFilter(string selectExpr, int width, int height, string? assFile)
    {
        var ratio = ((double)width / height).ToString("0.######", CultureInfo.InvariantCulture);
        // crop central: se a fonte é mais larga que a proporção alvo, corta largura; senão, corta altura.
        var crop = $"crop='if(gt(iw/ih,{ratio}),ih*{ratio},iw)':'if(gt(iw/ih,{ratio}),ih,iw/{ratio})'";
        var video = $"[0:v]select='{selectExpr}',setpts=N/FRAME_RATE/TB,{crop},scale={width}:{height}:flags=lanczos";
        if (assFile is not null) video += $",subtitles={assFile}";
        video += "[v]";
        var audio = $"[0:a]aselect='{selectExpr}',asetpts=N/SR/TB[a]";
        return video + ";" + audio;
    }
}
