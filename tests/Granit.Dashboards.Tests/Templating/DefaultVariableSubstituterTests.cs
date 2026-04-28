using Granit.Dashboards.Extensions;
using Granit.Dashboards.Templating;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Tests.Templating;

/// <summary>
/// Locks the <see cref="IVariableSubstituter"/> contract: <c>${var}</c> only, no
/// expressions, no scripting. Unknown variables become empty strings (warning
/// logged) — never throw.
/// </summary>
public sealed class DefaultVariableSubstituterTests
{
    private static IVariableSubstituter NewSubstituter()
    {
        ServiceCollection services = new();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        services.AddGranitDashboards();
        return services.BuildServiceProvider().GetRequiredService<IVariableSubstituter>();
    }

    [Fact]
    public void Substitute_ReplacesKnownVariable()
    {
        IVariableSubstituter substituter = NewSubstituter();
        Dictionary<string, object?> context = new()
        {
            ["entityName"] = "Acme Corp",
        };

        string? result = substituter.Substitute("Customer ${entityName} — invoices unpaid", context);

        result.ShouldBe("Customer Acme Corp — invoices unpaid");
    }

    [Fact]
    public void Substitute_ReplacesMultipleVariables()
    {
        IVariableSubstituter substituter = NewSubstituter();
        Dictionary<string, object?> context = new()
        {
            ["customer"] = "Acme",
            ["count"] = 12,
        };

        string? result = substituter.Substitute("${customer}: ${count} unpaid", context);

        result.ShouldBe("Acme: 12 unpaid");
    }

    [Fact]
    public void Substitute_UnknownVariable_BecomesEmpty_DoesNotThrow()
    {
        IVariableSubstituter substituter = NewSubstituter();
        Dictionary<string, object?> context = [];

        string? result = substituter.Substitute("Hello ${missing}!", context);

        result.ShouldBe("Hello !");
    }

    [Fact]
    public void Substitute_NullValue_BecomesEmpty()
    {
        IVariableSubstituter substituter = NewSubstituter();
        Dictionary<string, object?> context = new()
        {
            ["maybe"] = null,
        };

        string? result = substituter.Substitute("[${maybe}]", context);

        result.ShouldBe("[]");
    }

    [Fact]
    public void Substitute_NullInput_ReturnsNull()
    {
        IVariableSubstituter substituter = NewSubstituter();
        substituter.Substitute(null, new Dictionary<string, object?>()).ShouldBeNull();
    }

    [Fact]
    public void Substitute_EmptyInput_ReturnsEmpty()
    {
        IVariableSubstituter substituter = NewSubstituter();
        substituter.Substitute(string.Empty, new Dictionary<string, object?>()).ShouldBe(string.Empty);
    }

    [Fact]
    public void Substitute_NoPlaceholders_PassesThrough()
    {
        IVariableSubstituter substituter = NewSubstituter();
        substituter.Substitute("plain string", new Dictionary<string, object?>()).ShouldBe("plain string");
    }

    [Fact]
    public void Substitute_DoesNotEvaluateExpressions()
    {
        // The contract explicitly bans any syntax beyond literal lookup.
        // ${1+1} is not "math" — it's a literal variable name "1+1" that won't be in any context.
        IVariableSubstituter substituter = NewSubstituter();

        string? result = substituter.Substitute("${1+1}", new Dictionary<string, object?>());

        result.ShouldBe(string.Empty); // unknown variable, no eval
    }

    [Fact]
    public void Substitute_CaseSensitive()
    {
        IVariableSubstituter substituter = NewSubstituter();
        Dictionary<string, object?> context = new()
        {
            ["Name"] = "Acme",
        };

        substituter.Substitute("${name}", context).ShouldBe(string.Empty); // lowercase miss
        substituter.Substitute("${Name}", context).ShouldBe("Acme");
    }
}
