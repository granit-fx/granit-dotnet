using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core DbContext for API key persistence.
/// </summary>
internal sealed class AuthenticationApiKeysDbContext(
    DbContextOptions<AuthenticationApiKeysDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>
    /// API keys table.
    /// </summary>
    public DbSet<ApiKeyEntry> ApiKeys => Set<ApiKeyEntry>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureApiKeysModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
