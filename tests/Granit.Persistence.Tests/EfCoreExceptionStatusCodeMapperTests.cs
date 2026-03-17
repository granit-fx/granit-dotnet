// =============================================================================
// Tests - EfCoreExceptionStatusCodeMapper
// =============================================================================
// Verifies:
//   - DbUpdateConcurrencyException → 409 Conflict
//   - Other exceptions → null (delegation to next mapper)
//   - Conditional registration: mapper is registered when ExceptionHandling is configured
//   - No registration when ExceptionHandling is not configured
// =============================================================================

using Granit.Http.ExceptionHandling;
using Granit.Http.ExceptionHandling.Extensions;
using Granit.Persistence.ExceptionHandling;
using Granit.Persistence.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class EfCoreExceptionStatusCodeMapperTests
{
    // -------------------------------------------------------------------------
    // Unit tests: mapper logic
    // -------------------------------------------------------------------------

    [Fact]
    public void DbUpdateConcurrencyException_Returns409()
    {
        EfCoreExceptionStatusCodeMapper mapper = new();

        int? result = mapper.TryGetStatusCode(new DbUpdateConcurrencyException());

        result.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void InvalidOperationException_ReturnsNull()
    {
        EfCoreExceptionStatusCodeMapper mapper = new();

        int? result = mapper.TryGetStatusCode(new InvalidOperationException("unrelated"));

        result.ShouldBeNull("EfCoreMapper must delegate unknown exceptions to the next mapper");
    }

    [Fact]
    public void ArgumentException_ReturnsNull()
    {
        EfCoreExceptionStatusCodeMapper mapper = new();

        int? result = mapper.TryGetStatusCode(new ArgumentException("unrelated"));

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Integration: conditional DI registration
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitPersistence_WithExceptionHandling_RegistersEfCoreMapper()
    {
        ServiceCollection services = new();
        services.AddLogging();

        // ExceptionHandling registered first
        services.AddGranitExceptionHandling();
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();

        System.Collections.Generic.IEnumerable<IExceptionStatusCodeMapper> mappers =
            sp.GetServices<IExceptionStatusCodeMapper>();

        mappers.ShouldContain(m => m is EfCoreExceptionStatusCodeMapper,
            "EfCoreExceptionStatusCodeMapper must be registered when ExceptionHandling is active");
    }

    [Fact]
    public void AddGranitPersistence_WithoutExceptionHandling_DoesNotRegisterEfCoreMapper()
    {
        ServiceCollection services = new();
        services.AddLogging();

        // ExceptionHandling NOT registered
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();

        System.Collections.Generic.IEnumerable<IExceptionStatusCodeMapper> mappers =
            sp.GetServices<IExceptionStatusCodeMapper>();

        mappers.ShouldNotContain(m => m is EfCoreExceptionStatusCodeMapper,
            "EfCoreExceptionStatusCodeMapper must not be registered without ExceptionHandling");
    }
}
