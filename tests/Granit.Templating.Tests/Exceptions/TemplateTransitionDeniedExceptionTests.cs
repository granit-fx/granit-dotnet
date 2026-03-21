using Granit.Core.Exceptions;
using Granit.Templating.Exceptions;
using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Exceptions;

public sealed class TemplateTransitionDeniedExceptionTests
{
    [Fact]
    public void Constructor_SetsFromAndToProperties()
    {
        TemplateTransitionDeniedException ex = new(
            TemplateLifecycleStatus.Draft,
            TemplateLifecycleStatus.Archived);

        ex.From.ShouldBe(TemplateLifecycleStatus.Draft);
        ex.To.ShouldBe(TemplateLifecycleStatus.Archived);
    }

    [Fact]
    public void Constructor_MessageContainsFromAndTo()
    {
        TemplateTransitionDeniedException ex = new(
            TemplateLifecycleStatus.Published,
            TemplateLifecycleStatus.Draft);

        ex.Message.ShouldContain("Published");
        ex.Message.ShouldContain("Draft");
    }

    [Fact]
    public void Exception_IsConflictException()
    {
        TemplateTransitionDeniedException ex = new(
            TemplateLifecycleStatus.Draft,
            TemplateLifecycleStatus.Published);

        ex.ShouldBeAssignableTo<ConflictException>();
    }

    [Theory]
    [InlineData(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published)]
    [InlineData(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived)]
    [InlineData(TemplateLifecycleStatus.Archived, TemplateLifecycleStatus.Draft)]
    public void Constructor_AllTransitionCombinations_SetCorrectProperties(
        TemplateLifecycleStatus from, TemplateLifecycleStatus to)
    {
        TemplateTransitionDeniedException ex = new(from, to);

        ex.From.ShouldBe(from);
        ex.To.ShouldBe(to);
        ex.Message.ShouldContain("denied");
    }
}
