using Granit.Http.Cookies.EntityFrameworkCore.Extensions;
using Granit.Http.Cookies.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test the internal DbContext and services

namespace Granit.Http.Cookies.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies that <c>AddGranitCookiesEntityFrameworkCore</c> deterministically replaces the
/// base module's <see cref="NullConsentLedger"/> with <see cref="EfCoreConsentLedger"/> —
/// in BOTH wiring orders — and registers the erasure primitive and queryable source.
/// </summary>
public sealed class CookiesEntityFrameworkCoreRegistrationTests
{
    [Fact]
    public void AddGranitCookiesEntityFrameworkCore_AfterBaseModule_ReplacesNullLedger()
    {
        // Arrange — base module first (its TryAdd registers the Null default).
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddGranitCookies(_ => { });

        // Act
        builder.AddGranitCookiesEntityFrameworkCore(options => options.UseSqlite("DataSource=:memory:"));

        // Assert
        ResolveLedgerImplementation(builder.Services).ShouldBe(typeof(EfCoreConsentLedger));
    }

    [Fact]
    public void AddGranitCookiesEntityFrameworkCore_BeforeBaseModule_StillWinsOverNullLedger()
    {
        // Arrange — EF Core first; the base module's later TryAdd must no-op.
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.AddGranitCookiesEntityFrameworkCore(options => options.UseSqlite("DataSource=:memory:"));

        // Act
        builder.Services.AddGranitCookies(_ => { });

        // Assert
        ResolveLedgerImplementation(builder.Services).ShouldBe(typeof(EfCoreConsentLedger));
    }

    [Fact]
    public void AddGranitCookies_Alone_RegistersNullLedger()
    {
        // Arrange + Act
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddGranitCookies(_ => { });

        // Assert — silent-no-op rule: a default is always present.
        ResolveLedgerImplementation(builder.Services).ShouldBe(typeof(NullConsentLedger));
    }

    [Fact]
    public void AddGranitCookiesEntityFrameworkCore_RegistersEraserAndQueryableSource()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        // Act
        builder.AddGranitCookiesEntityFrameworkCore(options => options.UseSqlite("DataSource=:memory:"));

        // Assert
        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(ICookieConsentEraser)
            && d.ImplementationType == typeof(EfCoreCookieConsentEraser));
        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<CookieConsentRecord>)
            && d.ImplementationType == typeof(EfCookieConsentRecordQueryableSource));
        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<CookiesDbContext>));
    }

    private static Type? ResolveLedgerImplementation(IServiceCollection services)
    {
        ServiceDescriptor descriptor = services
            .Where(d => d.ServiceType == typeof(IConsentLedger))
            .ShouldHaveSingleItem();
        return descriptor.ImplementationType;
    }
}
