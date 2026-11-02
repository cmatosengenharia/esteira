using Esteira.Application.Publishing;

namespace Esteira.Application.Copy;

/// <summary>Entrada do template: hook.kind e cta da style bible do viral; promessa, tema e hashtags vêm do cliente.</summary>
public sealed record CopyInput(
    string HookKind,
    bool CtaPresent,
    string? CtaKind,
    string Promise,
    string Topic,
    IReadOnlyList<string> Hashtags);

/// <summary>Copy de uma rede. Title é null quando a rede não tem título (MaxTitleChars = 0).</summary>
public sealed record CopyDraft(string? Title, string Body, IReadOnlyList<string> Hashtags);

/// <summary>
/// Gancho + promessa + CTA do viral de referência viram o texto do post, dentro dos limites de network-specs.json.
/// Ideia (fórmula de gancho por tipo) de sergebulaev/linkedin-skills, reescrita do zero: ver THIRD_PARTY_NOTICES.md.
/// </summary>
public static class CopyTemplate
{
    private const string Separator = "\n\n";

    public static CopyDraft Build(CopyInput input, NetworkSpec spec)
    {
        var hook = Hook(input.HookKind, input.Topic);
        var cta = input.CtaPresent ? Cta(input.CtaKind) : null;
        var hashtags = input.Hashtags
            .Take(spec.RecommendedHashtags)
            .Select(t => t.StartsWith('#') ? t : "#" + t)
            .ToList();
        var tail = new List<string>();
        if (cta is not null) tail.Add(cta);
        if (hashtags.Count > 0) tail.Add(string.Join(' ', hashtags));

        var promise = input.Promise;
        if (spec.MaxCaptionChars > 0)
        {
            var fixedLength = string.Join(Separator, [hook, .. tail]).Length + Separator.Length;
            promise = Truncate(promise, spec.MaxCaptionChars - fixedLength);
        }

        var body = string.Join(Separator, new[] { hook, promise }.Concat(tail).Where(p => p.Length > 0));
        var title = spec.MaxTitleChars > 0 ? Truncate(hook, spec.MaxTitleChars) : null;
        return new CopyDraft(title, body, hashtags);
    }

    private static string Hook(string kind, string topic) => kind switch
    {
        "question" => $"Você já se perguntou como {topic}?",
        "number" => $"3 coisas sobre {topic} que ninguém te conta",
        "promise" => $"Em poucos minutos você vai dominar {topic}",
        "shock" => $"Quase todo mundo erra em {topic}",
        "visual" => $"Olha só o que acontece quando o assunto é {topic}",
        "story" => $"Deixa eu te contar como {topic} mudou tudo pra mim",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "hook.kind fora do contrato style-bible"),
    };

    private static string Cta(string? kind) => kind switch
    {
        "follow" => "Me segue para mais conteúdo assim.",
        "comment" => "Comenta aqui o que você achou.",
        "link" => "O link está na bio.",
        "dm" => "Me chama no direct.",
        "save" => "Salva este post para ver depois.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "cta.kind fora do contrato style-bible"),
    };

    /// <summary>Corta na última palavra inteira que cabe, com reticências; vazio se não cabe nada.</summary>
    private static string Truncate(string text, int max)
    {
        if (text.Length <= max) return text;
        if (max <= 1) return "";
        var cut = text[..(max - 1)];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0) cut = cut[..lastSpace];
        return cut.TrimEnd() + "…";
    }
}
