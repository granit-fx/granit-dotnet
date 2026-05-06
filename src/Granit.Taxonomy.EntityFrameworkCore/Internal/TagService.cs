using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="ITagService"/> — pure CRUD over the
/// <c>taxonomy_tags</c> table, scoped to the current tenant.
/// </summary>
/// <remarks>
/// Uniqueness on <c>(TenantId, Scope, Name)</c> is enforced by the database index
/// <c>ux_taxonomy_tags_tenant_scope_name</c>; <see cref="CreateAsync"/> performs a
/// best-effort pre-check to surface an <see cref="InvalidOperationException"/> with a
/// clear message before relying on the unique-violation surfacing
/// <see cref="DbUpdateException"/>.
/// </remarks>
internal sealed class TagService(
    IDbContextFactory<TaxonomyDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator) : ITagService
{
    /// <inheritdoc />
    public async Task<Tag> CreateAsync(
        string scope,
        string name,
        string color,
        bool hideOnEntityCard = false,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        bool exists = await context.Tags
            .AsNoTracking()
            .AnyAsync(
                t => t.TenantId == tenantId && t.Scope == scope && t.Name == name,
                cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException(
                $"A tag named '{name}' already exists in scope '{scope}' for this tenant.");
        }

        var tag = Tag.Create(
            guidGenerator.Create(),
            tenantId,
            scope,
            name,
            color,
            hideOnEntityCard);

        context.Tags.Add(tag);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tag;
    }

    /// <inheritdoc />
    public async Task<Tag?> RenameAsync(
        Guid id,
        string newName,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Tag? tag = await context.Tags
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (tag is null)
        {
            return null;
        }

        tag.Rename(newName);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tag;
    }

    /// <inheritdoc />
    public async Task<Tag?> RecolourAsync(
        Guid id,
        string newColor,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Tag? tag = await context.Tags
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (tag is null)
        {
            return null;
        }

        tag.Recolour(newColor);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tag;
    }

    /// <inheritdoc />
    public async Task<Tag?> ToggleHideAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Tag? tag = await context.Tags
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (tag is null)
        {
            return null;
        }

        tag.ToggleHideOnEntityCard();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tag;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Tag? tag = await context.Tags
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (tag is null)
        {
            return false;
        }

        tag.MarkDeleted();
        context.Tags.Remove(tag);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<Tag?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> ListByScopeAsync(
        string scope,
        string? q = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative.");
        }
        if (take is <= 0 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be in (0, 500].");
        }

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IQueryable<Tag> query = context.Tags.AsNoTracking();

        // "*" is the cross-scope sentinel used by the search endpoint (T3.1).
        if (!string.Equals(scope, "*", StringComparison.Ordinal))
        {
            query = query.Where(t => t.Scope == scope);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            string prefix = q.Trim();
            query = query.Where(t => EF.Functions.Like(t.Name, prefix + "%"));
        }

        return await query
            .OrderBy(t => t.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
