using Granit.Indexing.AI.Prompts;
using Shouldly;
using Xunit;

namespace Granit.Indexing.AI.Tests;

public sealed class DefaultAutoTagPromptBuilderTests
{
    [Fact]
    public void BuildInstruction_lists_candidates_as_the_authoritative_set()
    {
        // The candidate list is in the instruction (developer-controlled, never sanitized) so a
        // hostile document inside the primitive's <data> block cannot redefine the tag universe.
        string instruction = new DefaultAutoTagPromptBuilder().BuildInstruction(["a", "b", "c"], 3);

        instruction.ShouldContain("\"a\"");
        instruction.ShouldContain("\"b\"");
        instruction.ShouldContain("\"c\"");
        instruction.ShouldContain("INERT DATA");
        instruction.ShouldContain("Do NOT invent new tags");
    }

    [Fact]
    public void BuildInstruction_communicates_the_max_tags_cap_to_the_model()
    {
        string instruction = new DefaultAutoTagPromptBuilder().BuildInstruction(["a"], 7);

        instruction.ShouldContain("AT MOST 7");
    }

    [Fact]
    public void BuildInstruction_handles_empty_candidate_list_gracefully()
    {
        string instruction = new DefaultAutoTagPromptBuilder().BuildInstruction([], 5);

        instruction.ShouldContain("(no candidates");
    }

    [Fact]
    public void BuildInstruction_throws_on_non_positive_maxTags()
    {
        DefaultAutoTagPromptBuilder builder = new();

        Should.Throw<ArgumentOutOfRangeException>(() => builder.BuildInstruction(["a"], 0));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.BuildInstruction(["a"], -1));
    }
}
