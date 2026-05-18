using System.Linq.Expressions;
using System.Reflection;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity.Local.Domain;
using Granit.MultiTenancy;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the OpenIddict module, extending
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/> with OpenIddict entity support.
/// </summary>
/// <remarks>
/// <para>
/// Cannot inherit from <see cref="GranitDbContext"/> because the C# single-inheritance
/// constraint already binds this type to ASP.NET Identity's
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/>. The parameterised IMultiTenant
/// filter is therefore replicated inline (see <see cref="CurrentTenantId"/>,
/// <see cref="IsMultiTenantFilterEnabled"/>, <see cref="ConfigureMultiTenantFilter"/>) —
/// any change to the GranitDbContext filter shape must mirror here.
/// </para>
/// </remarks>
internal sealed class OpenIddictDbContext(
    DbContextOptions<OpenIddictDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null,
    IOptions<MetadataMappingOptions<LocalIdentity>>? extensionOptions = null)
    : IdentityDbContext<LocalIdentity, GranitRole, Guid>(options)
{
    private static readonly MethodInfo ConfigureMultiTenantFilterMethod =
        typeof(OpenIddictDbContext).GetMethod(
            nameof(ConfigureMultiTenantFilter),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ICurrentTenant _currentTenant = currentTenant
        ?? throw new ArgumentNullException(nameof(currentTenant));
    private readonly IDataFilter? _dataFilter = dataFilter;

    /// <inheritdoc cref="GranitDbContext.CurrentTenantId" />
    public Guid? CurrentTenantId => _currentTenant.IsAvailable ? _currentTenant.Id : null;

    /// <inheritdoc cref="GranitDbContext.IsMultiTenantFilterEnabled" />
    public bool IsMultiTenantFilterEnabled => _dataFilter?.IsEnabled<IMultiTenant>() ?? true;

    /// <summary>Gets the user groups set.</summary>
    public DbSet<GranitUserGroup> UserGroups => Set<GranitUserGroup>();

    /// <summary>Gets the user group members set.</summary>
    public DbSet<GranitUserGroupMember> UserGroupMembers => Set<GranitUserGroupMember>();

    /// <summary>Gets the signing keys set.</summary>
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 1. ASP.NET Identity conventions (default table names, keys, indexes).
        base.OnModelCreating(builder);

        // 2. Granit OpenIddict conventions (Identity keys, OpenIddict conventions,
        //    openiddict_* table prefix, column constraints, manual soft-delete filter)
        builder.ConfigureOpenIddictModule(_dataFilter, extensionOptions?.Value);

        // 3. Granit cross-cutting conventions WITHOUT the IMultiTenant filter (currentTenant: null).
        builder.ApplyGranitConventions(currentTenant: null, _dataFilter);

        // 4. Parameterised IMultiTenant filter — inlined here because we cannot inherit
        //    from GranitDbContext (see remarks on the class).
        foreach (IMutableEntityType entityType in builder.Model.GetEntityTypes()
            .Where(et => typeof(IMultiTenant).IsAssignableFrom(et.ClrType)))
        {
            ConfigureMultiTenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder]);
        }
    }

    private void ConfigureMultiTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class
    {
        Expression<Func<TEntity, bool>> filter = e =>
            !IsMultiTenantFilterEnabled
            || EF.Property<Guid?>(e, nameof(IMultiTenant.TenantId)) == CurrentTenantId;
        builder.Entity<TEntity>()
            .HasQueryFilter(GranitFilterNames.MultiTenant, filter);
    }
}
