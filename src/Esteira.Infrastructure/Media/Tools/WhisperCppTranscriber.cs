using System.Text.Json;
using Esteira.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esteira.Infrastructure.Media.Tools;

/// <summary>
/// Chama o binário local do whisper.cpp (MIT, dependência de runtime permitida) com saída JSON completa,
/// um segmento por palavra (-ml 1 -sow), e converte em <see cref="Transcript"/>.
/// </summary>
public sealed class WhisperCppTranscriber(ProcessRunner runner, IOptions<MediaOptions> options, ILogger<WhisperCppTranscriber> logger) : ITranscriber
{
    private readonly MediaOptions _o = options.Value;

    public async Task<Transcript> TranscribeAsync(string wavLocalPath, string? languageHint, CancellationToken ct)
    {
        var bin = ResolveBinary();
        var model = Path.GetFullPath(_o.WhisperModelPath);
        if (!File.Exists(model)) throw new FileNotFoundException($"Modelo do whisper não encontrado: {model}. Rode scripts/check-env.sh.", model);

        var outPrefix = Path.Combine(Path.GetDirectoryName(wavLocalPath)!, Path.GetFileNameWithoutExtension(wavLocalPath) + ".whisper");
        var args = new List<string>
        {
            "-m", model, "-f", wavLocalPath, "-ojf", "-of", outPrefix,
            "-ml", "1", "-sow", "-t", _o.WhisperThreads.ToString(), "-l", string.IsNullOrWhiteSpace(languageHint) ? (string.IsNullOrWhiteSpace(_o.WhisperLanguage) ? "auto" : _o.WhisperLanguage) : languageHint,
            "-np",
        };
        var result = await runner.RunAsync(bin, args, ct);
        if (!result.Succeeded) throw new InvalidOperationException($"whisper.cpp falhou ({result.ExitCode}): {ProcessRunner.Tail(result.StdErr)}");

        var jsonPath = outPrefix + ".json";
        if (!File.Exists(jsonPath)) throw new InvalidOperationException($"whisper.cpp não gerou {jsonPath}");
        var json = await File.ReadAllTextAsync(jsonPath, ct);
        var transcript = Parse(json);
        logger.LogInformation("whisper: {Words} palavras, idioma {Lang}, {Ms} ms", transcript.Words.Count, transcript.Language, result.Elapsed.TotalMilliseconds);
        return transcript;
    }

    private string ResolveBinary()
    {
        if (!string.IsNullOrWhiteSpace(_o.WhisperPath)) return _o.WhisperPath;
        var env = Environment.GetEnvironmentVariable("ESTEIRA_WHISPER_BIN");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        foreach (var candidate in new[] { "whisper-cli", "whisper-cpp" })
            if (ExistsOnPath(candidate)) return candidate;
        throw new InvalidOperationException("whisper.cpp não encontrado (whisper-cli/whisper-cpp). brew install whisper-cpp");
    }

    private static bool ExistsOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator).Any(dir => File.Exists(Path.Combine(dir, name)));
    }

    /// <summary>Formato do -ojf: { result: { language }, transcription: [ { offsets: { from, to }, text, tokens: [ { text, p } ] } ] }</summary>
    public static Transcript Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var language = root.TryGetProperty("result", out var r) && r.TryGetProperty("language", out var l) ? l.GetString() ?? "auto" : "auto";
        var words = new List<TranscriptWord>();
        foreach (var seg in root.GetProperty("transcription").EnumerateArray())
        {
            var text = seg.GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrEmpty(text) || text.StartsWith("[_")) continue;
            var offsets = seg.GetProperty("offsets");
            var start = offsets.GetProperty("from").GetInt64() / 1000.0;
            var end = offsets.GetProperty("to").GetInt64() / 1000.0;
            double score = 1;
            if (seg.TryGetProperty("tokens", out var tokens))
            {
                var ps = tokens.EnumerateArray()
                    .Where(t => !(t.GetProperty("text").GetString() ?? "").StartsWith("[_"))
                    .Select(t => t.TryGetProperty("p", out var p) ? p.GetDouble() : 1.0).ToList();
                if (ps.Count > 0) score = ps.Average();
            }
            // "-ml 1 -sow" normalmente dá uma palavra por segmento; se vierem várias, distribui o tempo linearmente.
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var step = parts.Length == 0 ? 0 : (end - start) / parts.Length;
            for (var i = 0; i < parts.Length; i++)
                words.Add(new TranscriptWord(parts[i], Math.Round(start + i * step, 3), Math.Round(start + (i + 1) * step, 3), Math.Round(score, 4), null));
        }
        return new Transcript(language, words);
    }
}
