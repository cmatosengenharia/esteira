using System.Globalization;
using System.Text;
using Esteira.Infrastructure.Media.Planning;

namespace Esteira.Infrastructure.Media.Captions;

/// <summary>Gera legendas ASS (libass via filtro `subtitles` do ffmpeg) a partir das frases e do estilo do EDL.</summary>
public static class AssWriter
{
    public static string Write(IReadOnlyList<CaptionPhrase> phrases, CaptionStyle style, int playResX, int playResY)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Script Info]");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine(CultureInfo.InvariantCulture, $"PlayResX: {playResX}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"PlayResY: {playResY}");
        sb.AppendLine("WrapStyle: 0");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine();
        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        var alignment = style.Position switch { "top" => 8, "center" => 5, _ => 2 };
        var marginV = style.Position == "center" ? 0 : (int)(playResY * 0.12);
        var outline = style.Outline ? Math.Max(2, style.FontSize / 16) : 0;
        var shadow = style.Outline ? Math.Max(1, style.FontSize / 32) : 0;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Style: Default,{style.Font},{style.FontSize},{ToAssColor(style.InactiveColor)},{ToAssColor(style.ActiveColor)},&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,{outline},{shadow},{alignment},60,60,{marginV},1");
        sb.AppendLine();
        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        foreach (var p in phrases)
        {
            if (p.End <= p.Start) continue;
            sb.AppendLine(CultureInfo.InvariantCulture, $"Dialogue: 0,{Time(p.Start)},{Time(p.End)},Default,,0,0,0,,{Escape(p.Text)}");
        }
        return sb.ToString();
    }

    /// <summary>h:mm:ss.cc</summary>
    public static string Time(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}");
    }

    /// <summary>#RRGGBB → &amp;H00BBGGRR (ASS é BGR com alfa na frente).</summary>
    public static string ToAssColor(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6) return "&H00FFFFFF";
        return $"&H00{h[4..6]}{h[2..4]}{h[0..2]}".ToUpperInvariant();
    }

    private static string Escape(string text) => text.Replace("{", "(").Replace("}", ")").Replace("\n", "\\N");
}
