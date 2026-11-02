using System.Globalization;

namespace Esteira.Application.Billing;

/// <summary>Plano de assinatura: preço mensal em reais e teto de minutos de render no mês.</summary>
public sealed record Plan(string Id, string Name, decimal MonthlyPriceBrl, int RenderMinutesPerMonth);

/// <summary>
/// Planos lidos da tabela `## Planos` de docs/oferta.md (embutido no assembly); landing e cobrança leem daqui.
/// Ideias de posicionamento e preço de wondelai/skills, reescritas do zero: ver THIRD_PARTY_NOTICES.md.
/// </summary>
public static class PlanCatalog
{
    private const string OfferResource = "offer/oferta.md";
    private const string PlansHeading = "## Planos";

    public static IReadOnlyList<Plan> Default { get; } = Parse(ReadOfferDocument());

    public static string ReadOfferDocument()
    {
        using var stream = typeof(PlanCatalog).Assembly.GetManifestResourceStream(OfferResource)
            ?? throw new InvalidOperationException($"Recurso embutido não encontrado: {OfferResource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<Plan> Parse(string markdown)
    {
        var lines = markdown.Split('\n').Select(l => l.Trim()).ToList();
        var start = lines.IndexOf(PlansHeading);
        if (start < 0)
            throw new InvalidOperationException($"Seção '{PlansHeading}' não encontrada na oferta.");

        var pt = CultureInfo.GetCultureInfo("pt-BR");
        return lines
            .Skip(start + 1)
            .TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal))
            .Where(l => l.StartsWith('|'))
            .Skip(2) // cabeçalho e separador
            .Select(l => l.Trim('|').Split('|').Select(c => c.Trim()).ToArray())
            .Select(c => new Plan(c[0], c[1], decimal.Parse(c[2], pt), int.Parse(c[3], CultureInfo.InvariantCulture)))
            .ToList();
    }
}
