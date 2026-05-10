using System.Collections.Generic;
using Granit.Documents.Renditions.BackgroundJobs.Policies;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.BackgroundJobs.Tests;

public sealed class DefaultRenditionTypePolicyTests
{
    private static DefaultRenditionTypePolicy Build() =>
        new(Microsoft.Extensions.Options.Options.Create(new GranitRenditionsOptions()));

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("IMAGE/WEBP")]
    public void Images_yield_thumbnail_and_web(string contentType)
    {
        IReadOnlyList<RenditionTarget> targets = Build().ResolveTargets(contentType);

        targets.Count.ShouldBe(2);
        targets[0].Type.ShouldBe(RenditionType.Thumbnail);
        targets[1].Type.ShouldBe(RenditionType.Web);
        targets[1].TargetContentType.ShouldBe("image/webp");
    }

    [Fact]
    public void Pdf_yields_thumbnail_only()
    {
        IReadOnlyList<RenditionTarget> targets = Build().ResolveTargets("application/pdf");

        targets.Count.ShouldBe(1);
        targets[0].Type.ShouldBe(RenditionType.Thumbnail);
    }

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("application/msword")]
    [InlineData("application/vnd.ms-excel")]
    [InlineData("application/vnd.ms-powerpoint")]
    public void Office_mimes_yield_thumbnail_only(string contentType)
    {
        IReadOnlyList<RenditionTarget> targets = Build().ResolveTargets(contentType);

        targets.Count.ShouldBe(1);
        targets[0].Type.ShouldBe(RenditionType.Thumbnail);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("text/plain")]
    [InlineData("application/json")]
    public void Unknown_mimes_yield_empty_set(string contentType) =>
        Build().ResolveTargets(contentType).ShouldBeEmpty();
}
