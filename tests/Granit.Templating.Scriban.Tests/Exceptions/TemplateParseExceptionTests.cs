using Granit.Templating.Scriban.Exceptions;
using Scriban;
using Scriban.Parsing;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests.Exceptions;

public sealed class TemplateParseExceptionTests
{
    [Fact]
    public void Constructor_SetsErrorsProperty()
    {
        var parsed = Template.Parse("{{ if }}");
        IReadOnlyList<LogMessage> errors = parsed.Messages;

        TemplateParseException ex = new(errors);

        ex.Errors.ShouldBe(errors);
        ex.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Message_ContainsErrorDetails()
    {
        var parsed = Template.Parse("{{ if }}");

        TemplateParseException ex = new(parsed.Messages);

        ex.Message.ShouldContain("Failed to parse Scriban template");
    }

    [Fact]
    public void Exception_IsSystemException()
    {
        var parsed = Template.Parse("{{ if }}");

        TemplateParseException ex = new(parsed.Messages);

        ex.ShouldBeAssignableTo<Exception>();
    }
}
