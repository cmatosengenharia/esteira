using System.Text.Json;
using System.Text.Json.Nodes;
using Esteira.Application.Abstractions;
using Esteira.Application.Contracts;
using Esteira.Domain.Enums;
using Esteira.Infrastructure.Media.Audio;

namespace Esteira.Infrastructure.Media.Planning;

/// <summary>Estilo da camada de legendas, espelho de $defs/captionStyle do schema.</summary>
public sealed record CaptionStyle(
    string Mode = "phrase",
    int CombineWithinMs = 1200,
    int MaxCharsPerLine = 42,
    string Position = "bottom",
    string Font = "Arial",
    int FontSize = 64,
    string ActiveColor = "#FFD400",
    string InactiveColor = "#FFFFFF",
    bool Outline = true);

public sealed record EdlParams(
    double SplitGapSeconds = 0.6,
    double LeadSeconds = 0.08,
    double TrailSeconds = 0.12,
    double SilenceThresholdDb = -35,
    double MinSilenceSeconds = 0.5);

/// <summary>Leitura e escrita do EDL v1 (docs/contracts/edl.schema.json) sem depender de classes geradas.</summary>
public sealed class EdlDocument
{
    public required string SourceObjectKey { get; init; }
    public required double SourceDurationSeconds { get; init; }
    public required double SourceFps { get; init; }
    public string? StyleTemplateId { get; init; }
    public required string Strategy { get; init; }
    public required IReadOnlyList<TimeRange> Keep { get; init; }
    public IReadOnlyList<(string FirstWord, string LastWord, string Reason)> KeepAnnotations { get; init; } = [];
    public CaptionStyle? Captions { get; init; }
    public required EdlParams Params { get; init; }
    public required IReadOnlyList<(AspectRatio Aspect, string Reframe)> Outputs { get; init; }
    public double? TargetDurationSeconds { get; init; }

    public string ToJson()
    {
        var keep = new JsonArray();
        for (var i = 0; i < Keep.Count; i++)
        {
            var k = new JsonObject { ["in"] = Keep[i].In, ["out"] = Keep[i].Out };
            if (i < KeepAnnotations.Count)
            {
                var (first, last, reason) = KeepAnnotations[i];
                if (first.Length > 0) k["firstWord"] = first;
                if (last.Length > 0) k["lastWord"] = last;
                k["reason"] = reason;
            }
            keep.Add(k);
        }

        var layers = new JsonArray();
        if (Captions is { } c)
        {
            layers.Add(new JsonObject
            {
                ["type"] = "captions",
                ["style"] = new JsonObject
                {
                    ["mode"] = c.Mode, ["combineWithinMs"] = c.CombineWithinMs, ["maxCharsPerLine"] = c.MaxCharsPerLine,
                    ["position"] = c.Position, ["font"] = c.Font, ["fontSize"] = c.FontSize,
                    ["activeColor"] = c.ActiveColor, ["inactiveColor"] = c.InactiveColor, ["outline"] = c.Outline,
                },
            });
        }

        var outputs = new JsonArray();
        foreach (var (aspect, reframe) in Outputs)
            outputs.Add(new JsonObject { ["aspect"] = aspect.ToContract(), ["reframe"] = reframe });

        var root = new JsonObject
        {
            ["version"] = 1,
            ["source"] = new JsonObject { ["objectKey"] = SourceObjectKey, ["durationSeconds"] = SourceDurationSeconds, ["fps"] = SourceFps },
            ["styleTemplateId"] = StyleTemplateId,
            ["strategy"] = Strategy,
            ["keep"] = keep,
            ["layers"] = layers,
            ["params"] = new JsonObject
            {
                ["splitGapSeconds"] = Params.SplitGapSeconds, ["leadSeconds"] = Params.LeadSeconds, ["trailSeconds"] = Params.TrailSeconds,
                ["silenceThresholdDb"] = Params.SilenceThresholdDb, ["minSilenceSeconds"] = Params.MinSilenceSeconds,
            },
            ["outputs"] = outputs,
        };
        if (TargetDurationSeconds is { } target)
            root["verify"] = new JsonObject { ["targetDurationSeconds"] = target };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static EdlDocument Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var source = r.GetProperty("source");
        var keep = r.GetProperty("keep").EnumerateArray()
            .Select(k => new TimeRange(k.GetProperty("in").GetDouble(), k.GetProperty("out").GetDouble())).ToList();

        CaptionStyle? captions = null;
        foreach (var layer in r.GetProperty("layers").EnumerateArray())
        {
            if (layer.GetProperty("type").GetString() != "captions") continue;
            var s = layer.GetProperty("style");
            var d = new CaptionStyle();
            captions = new CaptionStyle(
                Str(s, "mode") ?? d.Mode, Int(s, "combineWithinMs") ?? d.CombineWithinMs, Int(s, "maxCharsPerLine") ?? d.MaxCharsPerLine,
                Str(s, "position") ?? d.Position, Str(s, "font") ?? d.Font, Int(s, "fontSize") ?? d.FontSize,
                Str(s, "activeColor") ?? d.ActiveColor, Str(s, "inactiveColor") ?? d.InactiveColor, Bool(s, "outline") ?? d.Outline);
        }

        var pd = new EdlParams();
        var p = r.TryGetProperty("params", out var pe) ? pe : default;
        var prms = p.ValueKind == JsonValueKind.Object
            ? new EdlParams(Dbl(p, "splitGapSeconds") ?? pd.SplitGapSeconds, Dbl(p, "leadSeconds") ?? pd.LeadSeconds, Dbl(p, "trailSeconds") ?? pd.TrailSeconds,
                Dbl(p, "silenceThresholdDb") ?? pd.SilenceThresholdDb, Dbl(p, "minSilenceSeconds") ?? pd.MinSilenceSeconds)
            : pd;

        var outputs = r.GetProperty("outputs").EnumerateArray()
            .Select(o => (AspectRatioExtensions.FromContract(o.GetProperty("aspect").GetString()!), Str(o, "reframe") ?? "face-track")).ToList();

        return new EdlDocument
        {
            SourceObjectKey = source.GetProperty("objectKey").GetString()!,
            SourceDurationSeconds = source.GetProperty("durationSeconds").GetDouble(),
            SourceFps = source.GetProperty("fps").GetDouble(),
            StyleTemplateId = Str(r, "styleTemplateId"),
            Strategy = Str(r, "strategy") ?? string.Empty,
            Keep = keep,
            Captions = captions,
            Params = prms,
            Outputs = outputs,
            TargetDurationSeconds = r.TryGetProperty("verify", out var v) ? Dbl(v, "targetDurationSeconds") : null,
        };
    }

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int? Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    private static double? Dbl(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
    private static bool? Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : null;
}
