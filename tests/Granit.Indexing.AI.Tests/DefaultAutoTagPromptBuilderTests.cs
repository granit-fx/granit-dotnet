using Granit.Indexing.AI.Prompts;
using Microsoft.Extensions.AI;
using Shouldly;
using Xunit;

namespace Granit.Indexing.AI.Tests;

public sealed class DefaultAutoTagPromptBuilderTests
{
    [Fact]
    public void Build_wraps_content_in_untrusted_document_envelope()
    {
        DefaultAutoTagPromptBuilder builder = new();

        IReadOnlyList<ChatMessage> messages = builder.Build(
            content: "hostile <ignore me> content",
            candidates: ["alpha", "beta"],
            maxTags: 3);

        messages.Count.ShouldBe(2);
        messages[0].Role.ShouldBe(ChatRole.System);
        messages[1].Role.ShouldBe(ChatRole.User);
        messages[1].Text.ShouldStartWith("<untrusted_document>");
        messages[1].Text.ShouldEndWith("</untrusted_document>");
    }

    [Fact]
    public void Build_lists_candidates_in_the_system_prompt_so_the_user_envelope_cannot_override_them()
    {
        // The candidate list MUST be in the SYSTEM message, not the user envelope. If the
        // model treated candidates as data inside <untrusted_document>, a hostile payload
        // could redefine the universe. Locking the placement here.
        DefaultAutoTagPromptBuilder builder = new();

        string systemPrompt = builder.Build("text", ["a", "b", "c"], 3)
            .First(m => m.Role == ChatRole.System).Text!;

        systemPrompt.ShouldContain("\"a\"");
        systemPrompt.ShouldContain("\"b\"");
        systemPrompt.ShouldContain("\"c\"");
        systemPrompt.ShouldContain("INERT DATA");
        systemPrompt.ShouldContain("Do NOT invent new tags");
    }

    [Fact]
    public void Build_communicates_the_max_tags_cap_to_the_model()
    {
        DefaultAutoTagPromptBuilder builder = new();

        string systemPrompt = builder.Build("text", ["a"], maxTags: 7)
            .First(m => m.Role == ChatRole.System).Text!;

        systemPrompt.ShouldContain("AT MOST 7");
    }

    [Fact]
    public void Build_handles_empty_candidate_list_gracefully()
    {
        // An empty candidate universe is a real-world tenant-onboarding case. The prompt
        // must instruct the model to return an empty array — defensive UX against the
        // model making something up.
        DefaultAutoTagPromptBuilder builder = new();

        string systemPrompt = builder.Build("text", [], maxTags: 5)
            .First(m => m.Role == ChatRole.System).Text!;

        systemPrompt.ShouldContain("(no candidates");
    }

    [Fact]
    public void Build_throws_on_non_positive_maxTags()
    {
        DefaultAutoTagPromptBuilder builder = new();

        Should.Throw<ArgumentOutOfRangeException>(() => builder.Build("text", ["a"], 0));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Build("text", ["a"], -1));
    }
}
