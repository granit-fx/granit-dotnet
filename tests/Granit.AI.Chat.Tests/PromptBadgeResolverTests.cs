using Granit.AI.Chat.Internal;
using Granit.AI.Prompts;
using Granit.AI.Prompts.Domain;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class PromptBadgeResolverTests
{
    private static readonly Guid Owner = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IPromptTemplateStore _store = Substitute.For<IPromptTemplateStore>();

    private PromptBadgeResolver CreateResolver() => new(_store);

    [Fact]
    public async Task Composes_prompt_contents_then_free_text_and_stamps_the_first_prompt()
    {
        var first = PromptTemplate.Create(Guid.NewGuid(), Owner, "Summarize", "d", "Summarise the content.");
        var second = PromptTemplate.Create(Guid.NewGuid(), Owner, "Tone", "d", "Use a formal tone.");
        _store.GetAsync(first.Id, Owner, Arg.Any<CancellationToken>()).Returns(first);
        _store.GetAsync(second.Id, Owner, Arg.Any<CancellationToken>()).Returns(second);

        PromptBadgeResolution result = await CreateResolver()
            .ResolveAsync([first.Id, second.Id], Owner, "my notes", TestContext.Current.CancellationToken);

        result.ComposedMessage.ShouldBe("Summarise the content.\n\nUse a formal tone.\n\nmy notes");
        result.PrimaryPromptName.ShouldBe("Summarize");
        result.PrimaryPromptVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Drops_references_that_resolve_to_nothing()
    {
        var known = PromptTemplate.Create(Guid.NewGuid(), Owner, "Known", "d", "Known content.");
        var missingId = Guid.NewGuid();
        _store.GetAsync(known.Id, Owner, Arg.Any<CancellationToken>()).Returns(known);
        _store.GetAsync(missingId, Owner, Arg.Any<CancellationToken>()).Returns((PromptTemplate?)null);

        PromptBadgeResolution result = await CreateResolver()
            .ResolveAsync([missingId, known.Id], Owner, "text", TestContext.Current.CancellationToken);

        result.ComposedMessage.ShouldBe("Known content.\n\ntext");
        result.PrimaryPromptName.ShouldBe("Known");
    }

    [Fact]
    public async Task A_badge_only_turn_composes_just_the_prompt_content()
    {
        var prompt = PromptTemplate.CreateSystem(Guid.NewGuid(), "Prompt:DailyBrief:Name", "d", "Build my daily brief.");
        _store.GetAsync(prompt.Id, Owner, Arg.Any<CancellationToken>()).Returns(prompt);

        PromptBadgeResolution result = await CreateResolver()
            .ResolveAsync([prompt.Id], Owner, "   ", TestContext.Current.CancellationToken);

        result.ComposedMessage.ShouldBe("Build my daily brief.");
        result.PrimaryPromptName.ShouldBe("Prompt:DailyBrief:Name");
    }

    [Fact]
    public async Task No_resolved_prompts_returns_the_message_unchanged_with_no_stamp()
    {
        var missingId = Guid.NewGuid();
        _store.GetAsync(missingId, Owner, Arg.Any<CancellationToken>()).Returns((PromptTemplate?)null);

        PromptBadgeResolution result = await CreateResolver()
            .ResolveAsync([missingId], Owner, "just text", TestContext.Current.CancellationToken);

        result.ComposedMessage.ShouldBe("just text");
        result.PrimaryPromptName.ShouldBeNull();
        result.PrimaryPromptVersion.ShouldBeNull();
    }
}
