// =============================================================================
// Tests - WolverineSqlServerOptions + WolverineSqlServerOptionsValidator
// =============================================================================
// Verifies default values, section name constant, and all validation branches.
// =============================================================================

using Granit.Wolverine.SqlServer.Internal;
using Granit.Wolverine.SqlServer.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Wolverine.Persistence;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests;

public sealed class WolverineSqlServerOptionsTests
{
    // -----------------------------------------------------------------------
    // WolverineSqlServerOptions — defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void SectionName_IsWolverineSqlServer() =>
        WolverineSqlServerOptions.SectionName.ShouldBe("Wolverine:SqlServer");

    [Fact]
    public void DefaultTransportConnectionString_IsNull() =>
        new WolverineSqlServerOptions().TransportConnectionString.ShouldBeNull();

    [Fact]
    public void DefaultTransportConnectionStringName_IsNull() =>
        new WolverineSqlServerOptions().TransportConnectionStringName.ShouldBeNull();

    [Fact]
    public void DefaultSchemaName_IsNull() =>
        new WolverineSqlServerOptions().SchemaName.ShouldBeNull();

    [Fact]
    public void DefaultTransactionMode_IsEager() =>
        new WolverineSqlServerOptions().TransactionMode.ShouldBe(TransactionMiddlewareMode.Eager);

    // -----------------------------------------------------------------------
    // WolverineSqlServerOptionsValidator — happy paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_ValidConnectionString_Succeeds()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionString = "Server=localhost;Database=test;User Id=sa;Password=pass;TrustServerCertificate=True",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ConnectionStringNameOnly_Succeeds()
    {
        // Aspire integration: the actual value is resolved from ConnectionStrings:{name}.
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionStringName = "catalog-db",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_LightweightMode_Succeeds()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionString = "Server=localhost;Database=test",
            TransactionMode = TransactionMiddlewareMode.Lightweight,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // WolverineSqlServerOptionsValidator — failures (neither value configured)
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_NullConnectionStringAndName_Fails()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new();

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("TransportConnectionString"));
        result.Failures.ShouldContain(x => x.Contains("TransportConnectionStringName"));
    }

    [Fact]
    public void Validate_EmptyConnectionStringAndName_Fails()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionString = string.Empty,
            TransportConnectionStringName = string.Empty,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("TransportConnectionString"));
    }

    [Fact]
    public void Validate_WhitespaceConnectionStringAndName_Fails()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionString = "   ",
            TransportConnectionStringName = "   ",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("ISO 27001"));
    }
}
