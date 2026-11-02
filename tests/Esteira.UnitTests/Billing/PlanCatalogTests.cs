using Esteira.Application.Billing;

namespace Esteira.UnitTests.Billing;

public class PlanCatalogTests
{
    [Fact]
    public void Default_has_three_plans_with_unique_ids()
    {
        var plans = PlanCatalog.Default;

        Assert.Equal(3, plans.Count);
        Assert.Equal(3, plans.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Default_price_and_minutes_grow_strictly()
    {
        var plans = PlanCatalog.Default;

        for (var i = 1; i < plans.Count; i++)
        {
            Assert.True(plans[i].MonthlyPriceBrl > plans[i - 1].MonthlyPriceBrl, $"preço de {plans[i].Id}");
            Assert.True(plans[i].RenderMinutesPerMonth > plans[i - 1].RenderMinutesPerMonth, $"minutos de {plans[i].Id}");
        }
    }

    [Fact]
    public void Parse_reads_plans_table()
    {
        const string markdown = """
            # Oferta

            ## Planos

            | id | nome | preço mensal (R$) | minutos de render/mês |
            | --- | --- | --- | --- |
            | a | Plano A | 10 | 5 |
            | b | Plano B | 20,50 | 15 |

            ## Outra seção
            """;

        var plans = PlanCatalog.Parse(markdown);

        Assert.Equal([new Plan("a", "Plano A", 10m, 5), new Plan("b", "Plano B", 20.50m, 15)], plans);
    }

    [Fact]
    public void Parse_without_plans_section_throws()
    {
        Assert.Throws<InvalidOperationException>(() => PlanCatalog.Parse("# Oferta\n\n## Valor único\n\ntexto"));
    }

    [Fact]
    public void Offer_document_has_positioning_sections()
    {
        var doc = PlanCatalog.ReadOfferDocument();

        Assert.Contains("## Alternativas", doc);
        Assert.Contains("editor freelancer", doc);
        Assert.Contains("CapCut", doc);
        Assert.Contains("Opus Clip", doc);
        Assert.Contains("## Valor único", doc);
        Assert.Contains("molde", doc);
        Assert.Contains("## Público inicial", doc);
        Assert.Contains("## Planos", doc);
    }
}
