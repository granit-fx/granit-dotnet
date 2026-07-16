using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the OpenIddict entities (applications, authorizations, scopes, tokens) and
/// the signing-key table.
/// </summary>
/// <remarks>
/// Built on <see cref="GranitDbContext"/>. Owns a disjoint set of tables from the sibling
/// <c>IdentityLocalDbContext</c> (users/roles/groups) in the same database, so the two isolated
/// contexts never collide. A relational-model pinning test guards that the composed schema stays
/// byte-identical to the pre-split consolidated model.
/// </remarks>
internal sealed class OpenIddictDbContext(
    DbContextOptions<OpenIddictDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Gets the signing keys set.</summary>
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    /// <summary>
    /// OpenIddict's null-tenant applications, scopes and tokens are global — visible under every
    /// tenant scope (the ClientId already carries the tenant, so isolation holds via app + subject).
    /// </summary>
    protected override bool TenantFilterTreatsNullAsGlobal => true;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The IMultiTenant filter and ApplyGranitConventions run in GranitDbContext.OnModelCreating
        // after this override.
        modelBuilder.ConfigureGranitOpenIddict();
    }
}
