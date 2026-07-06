using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class TranslationContextTests
{
    [Fact]
    public void AllValues_AreDefined()
    {
        TranslationContext[] values = Enum.GetValues<TranslationContext>();
        values.Length.ShouldBe(5);
    }
}
