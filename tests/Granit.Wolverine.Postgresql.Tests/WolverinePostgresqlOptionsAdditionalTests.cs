// =============================================================================
// Tests - WolverinePostgresqlOptions (additional coverage)
// =============================================================================
// Covers property setters and additional validation scenarios.
// =============================================================================

using Granit.Wolverine.Postgresql.Internal;
using Granit.Wolverine.Postgresql.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Wolverine.Persistence;
using Xunit;

namespace Granit.Wolverine.Postgresql.Tests;

public sealed class WolverinePostgresqlOptionsAdditionalTests
{
    [Fact]
    public void TransportConnectionString_CanBeSet()
    {
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionString = "Host=db;Database=test",
        };

        options.TransportConnectionString.ShouldBe("Host=db;Database=test");
    }

    [Fact]
    public void TransportConnectionStringName_CanBeSet()
    {
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionStringName = "catalog-db",
        };

        options.TransportConnectionStringName.ShouldBe("catalog-db");
    }

    [Fact]
    public void TransactionMode_CanBeSetToLightweight()
    {
        WolverinePostgresqlOptions options = new()
        {
            TransactionMode = TransactionMiddlewareMode.Lightweight,
        };

        options.TransactionMode.ShouldBe(TransactionMiddlewareMode.Lightweight);
    }

    [Fact]
    public void Validate_WhitespaceConnectionStringName_Fails()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionStringName = "   ",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyConnectionStringName_Fails()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionStringName = string.Empty,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_FailureMessage_ContainsISO27001Reference()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new();

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("ISO 27001"));
    }

    [Fact]
    public void Validate_FailureMessage_ContainsAspireReference()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new();

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("Aspire"));
    }
}
