using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Esteira.Infrastructure.Media.Tools;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, TimeSpan Elapsed)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Executa ffmpeg/ffprobe/whisper com captura de saída e cancelamento.</summary>
public sealed class ProcessRunner(ILogger<ProcessRunner> logger)
{
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> args, CancellationToken ct, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        logger.LogDebug("exec {File} {Args}", fileName, string.Join(' ', args.Select(Quote)));
        var sw = Stopwatch.StartNew();
        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Não foi possível iniciar '{fileName}'. Está instalado e no PATH?", ex);
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* já encerrado */ }
            throw;
        }
        sw.Stop();
        var result = new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), sw.Elapsed);
        if (!result.Succeeded)
            logger.LogWarning("{File} saiu com {Code} em {Ms} ms: {Err}", fileName, result.ExitCode, sw.ElapsedMilliseconds, Tail(result.StdErr));
        return result;
    }

    public static string Tail(string text, int maxChars = 1500) => text.Length <= maxChars ? text : "…" + text[^maxChars..];

    private static string Quote(string a) => a.Contains(' ') ? $"\"{a}\"" : a;
}
