using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Events;
using Microsoft.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="ICategoryService"/>. <see cref="MoveAsync"/>
/// re-materialises descendant paths in a single <c>ExecuteUpdate</c>, mirroring
/// <c>FolderService.MoveAsync</c> from <c>Granit.Documents</c>.
/// </summary>
internal sealed class CategoryService(
    IDbContextFactory<TaxonomyDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    ILocalEventBus localEventBus,
    TaxonomyMetrics metrics) : ICategoryService
{
    /// <inheritdoc />
    public async Task<Category> CreateAsync(
        string scope,
        Guid? parentId,
        string name,
        string? iconName = null,
        bool hideOnEntityCard = false,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        Category category;

        if (parentId is { } parentGuid)
        {
            Category? parent = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == parentGuid, cancellationToken)
                .ConfigureAwait(false);
            if (parent is null)
            {
                throw new InvalidOperationException(
                    $"Parent category {parentGuid} was not found.");
            }
            if (!string.Equals(parent.Scope, scope, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Parent category {parentGuid} is in scope '{parent.Scope}', not '{scope}'.");
            }
            category = Category.Create(
                guidGenerator.Create(), parent, name, iconName, hideOnEntityCard);
        }
        else
        {
            category = Category.CreateRoot(
                guidGenerator.Create(), tenantId, scope, name, iconName, hideOnEntityCard);
        }

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        metrics.RecordCategoryCreated(tenantId?.ToString(), scope);
        return category;
    }

    /// <inheritdoc />
    public async Task<Category?> RenameAsync(
        Guid id,
        string newName,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Category? category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            return null;
        }

        string oldPath = category.Path;
        category.Rename(newName);
        string newPath = category.Path;
        int affectedDescendants = 0;

        if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            affectedDescendants = await ReMaterialiseDescendantsAsync(
                context, category, oldPath, newPath, depthDelta: 0, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            await localEventBus.PublishAsync(
                new CategoryTreePathChangedEvent(
                    category.Id, category.TenantId, oldPath, newPath, affectedDescendants),
                cancellationToken).ConfigureAwait(false);
        }

        return category;
    }

    /// <inheritdoc />
    public async Task<Category?> MoveAsync(
        Guid id,
        Guid? newParentId,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Category? category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            return null;
        }

        Category? newParent = null;
        if (newParentId is { } parentGuid)
        {
            newParent = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == parentGuid, cancellationToken)
                .ConfigureAwait(false);
            if (newParent is null)
            {
                throw new InvalidOperationException(
                    $"Target parent category {parentGuid} was not found.");
            }
        }

        string oldPath = category.Path;
        int oldDepth = category.Depth;

        category.MoveTo(newParent);

        string newPath = category.Path;
        int newDepth = category.Depth;
        int affectedDescendants = 0;

        if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            int depthDelta = newDepth - oldDepth;
            affectedDescendants = await ReMaterialiseDescendantsAsync(
                context, category, oldPath, newPath, depthDelta, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            await localEventBus.PublishAsync(
                new CategoryTreePathChangedEvent(
                    category.Id, category.TenantId, oldPath, newPath, affectedDescendants),
                cancellationToken).ConfigureAwait(false);
        }

        return category;
    }

    /// <inheritdoc />
    public async Task<Category?> SetIconAsync(
        Guid id,
        string? iconName,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Category? category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            return null;
        }

        category.SetIcon(iconName);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return category;
    }

    /// <inheritdoc />
    public async Task<Category?> ToggleHideAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Category? category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            return null;
        }

        category.ToggleHideOnEntityCard();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return category;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Category? category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            return false;
        }

        bool hasChildren = await context.Categories
            .AnyAsync(c => c.ParentId == id, cancellationToken)
            .ConfigureAwait(false);
        if (hasChildren)
        {
            throw new InvalidOperationException(
                $"Category {id} has descendants. Move or delete them first.");
        }

        category.MarkDeleted();
        context.Categories.Remove(category);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        metrics.RecordCategoryDeleted(category.TenantId?.ToString(), category.Scope);
        return true;
    }

    /// <inheritdoc />
    public async Task<Category?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> ListByScopeAsync(
        string scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Categories
            .AsNoTracking()
            .Where(c => c.Scope == scope)
            .OrderBy(c => c.Path)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Bulk-updates every descendant of <paramref name="category"/>: rewrites
    /// <c>Path</c> by replacing the old prefix with the new one and shifts
    /// <c>Depth</c> by <paramref name="depthDelta"/>. Single SQL <c>UPDATE</c>.
    /// </summary>
    private static async Task<int> ReMaterialiseDescendantsAsync(
        TaxonomyDbContext context,
        Category category,
        string oldPath,
        string newPath,
        int depthDelta,
        CancellationToken cancellationToken)
    {
        string descendantPrefix = oldPath + Category.PathSeparator;
        int oldPrefixLength = oldPath.Length;

        // CA1845 (Substring vs AsSpan) does not apply to EF Core expression trees:
        // ExecuteUpdateAsync translates Substring() to SQL SUBSTRING; AsSpan has no
        // SQL equivalent.
#pragma warning disable CA1845
        return await context.Categories
            .Where(c => c.TenantId == category.TenantId
                && c.Id != category.Id
                && c.Path.StartsWith(descendantPrefix))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Path, c => newPath + c.Path.Substring(oldPrefixLength))
                .SetProperty(c => c.Depth, c => c.Depth + depthDelta),
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA1845
    }
}
