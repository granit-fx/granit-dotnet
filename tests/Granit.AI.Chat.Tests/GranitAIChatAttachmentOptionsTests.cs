using Granit.AI.Chat.Options;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class GranitAIChatAttachmentOptionsTests
{
    [Fact]
    public void SectionName_matches_namespace_aligned_canonical_path()
    {
        // Locked here so a rename of the project / namespace surfaces in CI before
        // it silently breaks host appsettings.json bindings.
        GranitAIChatAttachmentOptions.SectionName.ShouldBe("AI:Chat:Attachments");
    }

    [Fact]
    public void Defaults_cap_attachment_count_and_size()
    {
        var options = new GranitAIChatAttachmentOptions();

        options.MaxAttachments.ShouldBe(5);
        options.MaxAttachmentBytes.ShouldBe(10 * 1024 * 1024);
        options.AllowedContentTypes.ShouldContain("application/pdf");
    }
}
