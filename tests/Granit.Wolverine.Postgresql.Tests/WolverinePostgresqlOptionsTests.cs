// =============================================================================
// Tests - WolverinePostgresqlOptions + WolverinePostgresqlOptionsValidator
// =============================================================================
// Verifies default values, section name constant, and all validation branches.
// =============================================================================

using Granit.Wolverine.Postgresql.Internal;
using Granit.Wolverine.Postgresql.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Wolverine.Persistence;
using Xunit;

namespace Granit.Wolverine.Postgresql.Tests;

public sealed class WolverinePostgresqlOptionsTests
{
    // -----------------------------------------------------------------------
    // WolverinePostgresqlOptions — defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void SectionName_IsWolverinePostgresql() =>
        WolverinePostgresqlOptions.SectionName.ShouldBe("Wolverine:Postgresql");

    [Fact]
    public void DefaultTransportConnectionString_IsNull() =>
        new WolverinePostgresqlOptions().TransportConnectionString.ShouldBeNull();

    [Fact]
    public void DefaultTransportConnectionStringName_IsNull() =>
        new WolverinePostgresqlOptions().TransportConnectionStringName.ShouldBeNull();

    [Fact]
    public void DefaultTransactionMode_IsEager() =>
        new WolverinePostgresqlOptions().TransactionMode.ShouldBe(TransactionMiddlewareMode.Eager);

    // -----------------------------------------------------------------------
    // WolverinePostgresqlOptionsValidator — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_ValidConnectionString_Succeeds()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionString = "Host=localhost;Database=test;Username=user;Password=pass",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ValidConnectionStringName_Succeeds()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionStringName = "catalog-db",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_BothConnectionStringAndName_Succeeds()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionString = "Host=localhost;Database=test",
            TransportConnectionStringName = "catalog-db",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_LightweightMode_Succeeds()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new()
        {
            TransportConnectionString = "Host=localhost;Database=test",
            TransactionMode = TransactionMiddlewareMode.Lightweight,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // WolverinePostgresqlOptionsValidator — failures
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_NeitherConnectionStringNorName_Fails()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new();

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("TransportConnectionString"));
        result.Failures.ShouldContain(x => x.Contains("TransportConnectionStringName"));
    }

    [Fact]
    public void Validate_EmptyConnectionStringAndNoName_Fails()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new() { TransportConnectionString = string.Empty };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhitespaceConnectionStringAndNoName_Fails()
    {
        WolverinePostgresqlOptionsValidator validator = new();
        WolverinePostgresqlOptions options = new() { TransportConnectionString = "   " };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }
}
