using Granit.Http.Cookies.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test the internal DbContext and services

namespace Granit.Http.Cookies.EntityFrameworkCore.Tests;

/// <summary>
/// Minimal <see cref="IDbContextFactory{TContext}"/> over pre-built options, so the
/// EF services under test run against the SQLite connection owned by the test class.
/// </summary>
internal sealed class StubCookiesDbContextFactory(
    DbContextOptions<CookiesDbContext> options,
    ICurrentTenant? currentTenant = null)
    : IDbContextFactory<CookiesDbContext>
{
    public CookiesDbContext CreateDbContext() =>
        new(options, currentTenant ?? GranitDesignTime.CurrentTenant);
}
