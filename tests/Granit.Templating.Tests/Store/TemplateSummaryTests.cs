using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Store;

public sealed class TemplateSummaryTests
{
    [Fact]
    public void AllProperties_CanBeSet()
    {
        DateTimeOffset lastModified = DateTimeOffset.UtcNow;

        TemplateSummary summary = new()
        {
            Name = "Billing.Invoice",
            Culture = "fr-BE",
            MimeType = "text/html",
            CurrentStatus = TemplateLifecycleStatus.Published,
            LastModifiedAt = lastModified,
            LastModifiedBy = "alice",
            HasPublishedVersion = true,
        };

        summary.Name.ShouldBe("Billing.Invoice");
        summary.Culture.ShouldBe("fr-BE");
        summary.MimeType.ShouldBe("text/html");
        summary.CurrentStatus.ShouldBe(TemplateLifecycleStatus.Published);
        summary.LastModifiedAt.ShouldBe(lastModified);
        summary.LastModifiedBy.ShouldBe("alice");
        summary.HasPublishedVersion.ShouldBeTrue();
    }

    [Fact]
    public void Culture_CanBeNull_ForNeutralTemplates()
    {
        TemplateSummary summary = new()
        {
            Name = "Billing.Invoice",
            Culture = null,
            MimeType = "text/html",
            CurrentStatus = TemplateLifecycleStatus.Draft,
            LastModifiedAt = DateTimeOffset.UtcNow,
            LastModifiedBy = "bob",
            HasPublishedVersion = false,
        };

        summary.Culture.ShouldBeNull();
        summary.HasPublishedVersion.ShouldBeFalse();
    }
}
