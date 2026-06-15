using Granit.TextExtraction.Tika.Options;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tika.Tests;

public sealed class TikaSidecarOptionsTests
{
    [Fact]
    public void Section_name_matches_convention() =>
        TikaSidecarOptions.SectionName.ShouldBe("TextExtraction:Tika");
}
