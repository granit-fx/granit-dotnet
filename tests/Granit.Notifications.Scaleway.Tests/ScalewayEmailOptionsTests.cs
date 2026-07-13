using Granit.Notifications.Scaleway.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Scaleway.Tests;

public sealed class ScalewayEmailOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        ScalewayEmailOptions.SectionName.ShouldBe("Notifications:Scaleway");

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        ScalewayEmailOptions options = new();

        options.SecretKey.ShouldBe(string.Empty);
        options.ProjectId.ShouldBe(string.Empty);
        options.DefaultSenderEmail.ShouldBe(string.Empty);
        options.DefaultSenderName.ShouldBe(string.Empty);
        options.Region.ShouldBe("fr-par");
        options.BaseUrl.ShouldBe("https://api.scaleway.com/transactional-email/v1alpha1");
        options.TimeoutSeconds.ShouldBe(30);
    }
}
