using Esteira.Application.Copy;
using Esteira.Application.Publishing;
using Esteira.Domain.Enums;

namespace Esteira.UnitTests.Copy;

public class CopyTemplateTests
{
    private static readonly NetworkSpecCatalog Catalog = new();

    private static readonly string[] Tags = ["edicao", "#viral", "reels", "dicas", "video", "criador", "shorts"];

    private static CopyInput Input(string hookKind = "question", bool ctaPresent = true, string? ctaKind = "comment",
        string promise = "Neste vídeo eu mostro o passo a passo que uso para cortar silêncio e legendar.") =>
        new(hookKind, ctaPresent, ctaKind, promise, "editar vídeos virais", Tags);

    [Fact]
    public void Instagram_question_hook_with_comment_cta()
    {
        var input = Input();
        var draft = CopyTemplate.Build(input, Catalog.Get(SocialNetwork.Instagram));

        Assert.Null(draft.Title);
        Assert.StartsWith("Você já se perguntou como editar vídeos virais?", draft.Body);
        Assert.Contains(input.Promise, draft.Body);
        Assert.Equal(["#edicao", "#viral", "#reels", "#dicas", "#video"], draft.Hashtags);
        Assert.EndsWith("Comenta aqui o que você achou.\n\n#edicao #viral #reels #dicas #video", draft.Body);
    }

    [Fact]
    public void X_cuts_long_promise_by_word_and_keeps_cta()
    {
        var longPromise = string.Join(' ', Enumerable.Repeat("palavra", 80));
        var draft = CopyTemplate.Build(Input(promise: longPromise), Catalog.Get(SocialNetwork.X));

        Assert.True(draft.Body.Length <= 280, $"{draft.Body.Length} > 280");
        Assert.Contains("palavra…", draft.Body);
        Assert.Contains("Comenta aqui o que você achou.", draft.Body);
        Assert.Equal(2, draft.Hashtags.Count);
    }

    [Fact]
    public void YouTube_title_comes_from_hook_within_limit()
    {
        var draft = CopyTemplate.Build(Input(hookKind: "number"), Catalog.Get(SocialNetwork.YouTube));

        Assert.Equal("3 coisas sobre editar vídeos virais que ninguém te conta", draft.Title);
        Assert.True(draft.Title!.Length <= 100);
    }

    [Theory]
    [InlineData(SocialNetwork.WhatsApp)]
    [InlineData(SocialNetwork.Telegram)]
    public void Networks_without_recommended_hashtags_get_none(SocialNetwork network)
    {
        var draft = CopyTemplate.Build(Input(), Catalog.Get(network));

        Assert.Empty(draft.Hashtags);
        Assert.DoesNotContain("#", draft.Body);
    }

    [Fact]
    public void No_cta_line_when_reference_has_no_cta()
    {
        var draft = CopyTemplate.Build(Input(ctaPresent: false, ctaKind: null), Catalog.Get(SocialNetwork.Instagram));

        Assert.DoesNotContain("Comenta", draft.Body);
        Assert.EndsWith("legendar.\n\n#edicao #viral #reels #dicas #video", draft.Body);
    }

    [Fact]
    public void Every_network_respects_caption_and_title_limits()
    {
        var longPromise = string.Join(' ', Enumerable.Repeat("palavra", 2000));
        foreach (var spec in Catalog.All)
        foreach (var kind in new[] { "question", "number", "promise", "shock", "visual", "story" })
        {
            var draft = CopyTemplate.Build(Input(hookKind: kind, ctaKind: "save", promise: longPromise), spec);
            if (spec.MaxCaptionChars > 0) Assert.True(draft.Body.Length <= spec.MaxCaptionChars, $"{spec.Network}/{kind}");
            if (spec.MaxTitleChars > 0) Assert.True(draft.Title!.Length <= spec.MaxTitleChars, $"{spec.Network}/{kind}");
            else Assert.Null(draft.Title);
            Assert.Contains("Salva este post para ver depois.", draft.Body);
        }
    }
}
