using Shouldly;

namespace Granit.Validation.AI.Tests;

public sealed class ModerationResultTests
{

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
