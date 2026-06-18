using Granit.AI.Tools.Internal;
using Shouldly;

namespace Granit.AI.Tools.Tests;

public sealed class ReasoningTextFilterTests
{
    // Feeds the fragments through one filter instance and returns the concatenated visible output,
    // mirroring how the orchestrator drives it over a stream of deltas.
    private static string Run(params string[] fragments)
    {
        ReasoningTextFilter filter = new();
        string streamed = string.Concat(fragments.Select(filter.Process));
        return streamed + filter.Flush();
    }

    [Fact]
    public void Passes_text_through_unchanged_when_there_is_no_think_block()
    {
        Run("The answer is 42.").ShouldBe("The answer is 42.");
    }

    [Fact]
    public void Strips_a_leading_think_block_and_the_blank_gap_it_leaves()
    {
        Run("<think>weighing options</think>\n\nThe answer is 42.").ShouldBe("The answer is 42.");
    }

    [Fact]
    public void Strips_a_think_block_split_across_several_deltas()
    {
        Run("<thi", "nk>secret reas", "oning</thi", "nk>visible").ShouldBe("visible");
    }

    [Fact]
    public void Strips_the_open_close_tags_when_each_straddles_a_delta_boundary()
    {
        Run("answer one <", "think>hidden</", "think> answer two")
            .ShouldBe("answer one  answer two");
    }

    [Fact]
    public void Keeps_text_that_only_looks_like_the_start_of_a_tag()
    {
        // "<thx" can never become "<think>", so the held tail must be released as ordinary text.
        Run("a <thx b").ShouldBe("a <thx b");
    }

    [Fact]
    public void Drops_an_unterminated_think_block()
    {
        Run("<think>reasoning that never closes").ShouldBe(string.Empty);
    }

    [Fact]
    public void Removes_multiple_think_blocks()
    {
        Run("<think>one</think>kept<think>two</think> end").ShouldBe("kept end");
    }

    [Fact]
    public void Matches_tags_case_insensitively()
    {
        Run("<THINK>x</Think>y").ShouldBe("y");
    }

    [Fact]
    public void Trims_only_the_leading_whitespace_not_internal_blank_lines()
    {
        Run("<think>r</think>\n\nline one\n\nline two").ShouldBe("line one\n\nline two");
    }
}
