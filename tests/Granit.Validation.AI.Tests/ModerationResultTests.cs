using Shouldly;
using Xunit;

namespace Granit.Validation.AI.Tests;

public sealed class ModerationResultTests
{
    [Fact]
    public void AcceptableResult_HasExpectedProperties()
    {
        ModerationResult result = new()
        {
            IsAcceptable = true,
            Flags = [],
        };

        result.IsAcceptable.ShouldBeTrue();
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public void FlaggedResult_ContainsFlags()
    {
        ModerationFlag flag = new(ModerationCategory.Toxic, "Offensive language", 0.9);

        ModerationResult result = new()
        {
            IsAcceptable = false,
            Flags = [flag],
        };

        result.IsAcceptable.ShouldBeFalse();
        result.Flags.Count.ShouldBe(1);
        result.Flags[0].Category.ShouldBe(ModerationCategory.Toxic);
        result.Flags[0].Description.ShouldBe("Offensive language");
        result.Flags[0].Severity.ShouldBe(0.9);
    }

    [Fact]
    public void ModerationFlag_RecordEquality()
    {
        ModerationFlag flag1 = new(ModerationCategory.Spam, "Spam content", 0.7);
        ModerationFlag flag2 = new(ModerationCategory.Spam, "Spam content", 0.7);

        flag1.ShouldBe(flag2);
    }

    [Fact]
    public void ModerationCategory_AllValues_AreDefined()
    {
        string[] categories = Enum.GetNames<ModerationCategory>();

        categories.ShouldContain("Toxic");
        categories.ShouldContain("Harassment");
        categories.ShouldContain("PromptInjection");
        categories.ShouldContain("Spam");
        categories.ShouldContain("Violence");
        categories.ShouldContain("SelfHarm");
        categories.ShouldContain("Sexual");
        categories.ShouldContain("Other");
        categories.Length.ShouldBe(8);
    }
}
