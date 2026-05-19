using Granit.Exceptions;
using Granit.Templating.Exceptions;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Exceptions;

public sealed class TemplateTransitionDeniedExceptionTests
{
    [Fact]
    public void Constructor_SetsFromAndToProperties()
    {
        TemplateTransitionDeniedException ex = new(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Archived);

        ex.From.ShouldBe(WorkflowLifecycleStatus.Draft);
        ex.To.ShouldBe(WorkflowLifecycleStatus.Archived);
    }

    [Fact]
    public void Constructor_MessageContainsFromAndTo()
    {
        TemplateTransitionDeniedException ex = new(
            WorkflowLifecycleStatus.Published,
            WorkflowLifecycleStatus.Draft);

        ex.Message.ShouldContain("Published");
        ex.Message.ShouldContain("Draft");
    }

    [Fact]
    public void Exception_IsConflictException()
    {
        TemplateTransitionDeniedException ex = new(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published);

        ex.ShouldBeAssignableTo<ConflictException>();
    }

    [Theory]
    [InlineData(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published)]
    [InlineData(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived)]
    [InlineData(WorkflowLifecycleStatus.Archived, WorkflowLifecycleStatus.Draft)]
    public void Constructor_AllTransitionCombinations_SetCorrectProperties(
        WorkflowLifecycleStatus from, WorkflowLifecycleStatus to)
    {
        TemplateTransitionDeniedException ex = new(from, to);

        ex.From.ShouldBe(from);
        ex.To.ShouldBe(to);
        ex.Message.ShouldContain("denied");
    }
}
