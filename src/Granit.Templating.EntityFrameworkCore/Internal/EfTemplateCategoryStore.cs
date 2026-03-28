using Granit.Domain;
using Granit.Exceptions;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.Store;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITemplateCategoryStoreReader"/> and <see cref="ITemplateCategoryStoreWriter"/>.
/// </summary>
internal sealed class EfTemplateCategoryStore(
    IDbContextFactory<TemplatingDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IClock clock,
    ICurrentTenant? currentTenant = null)
    : ITemplateCategoryStoreReader, ITemplateCategoryStoreWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TemplateCategory>> ListCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<TemplateCategoryEntity> entities = await ctx.TemplateCategories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Count templates per category in a single query.
        Dictionary<Guid, int> counts = await ctx.TemplateRevisions
            .Where(r => r.CategoryId != null)
            .GroupBy(r => r.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Select(r => r.TemplateName).Distinct().Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(e => ToCategory(e, counts.GetValueOrDefault(e.Id)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<TemplateCategory?> GetCategoryAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        TemplateCategoryEntity? entity = await ctx.TemplateCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        int templateCount = await ctx.TemplateRevisions
            .Where(r => r.CategoryId == id)
            .Select(r => r.TemplateName)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToCategory(entity, templateCount);
    }

    /// <inheritdoc/>
    public async Task<TemplateCategory> CreateCategoryAsync(
        string name,
        string? description,
        string? icon,
        int sortOrder,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        bool exists = await ctx.TemplateCategories
            .AnyAsync(c => c.Name == name, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new ConflictException(
                "TemplateCategory:DuplicateName",
                $"A category with the name '{name}' already exists.");
        }

        var entity = new TemplateCategoryEntity
        {
            Id = guidGenerator.Create(),
            Name = name,
            Description = description,
            Icon = icon,
            SortOrder = sortOrder,
            CreatedAt = clock.Now,
            CreatedBy = createdBy,
            TenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null,
        };

        ctx.TemplateCategories.Add(entity);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToCategory(entity, templateCount: 0);
    }

    /// <inheritdoc/>
    public async Task<TemplateCategory> UpdateCategoryAsync(
        Guid id,
        string name,
        string? description,
        string? icon,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        TemplateCategoryEntity? entity = await ctx.TemplateCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new EntityNotFoundException(typeof(TemplateCategoryEntity), id);
        }

        bool nameConflict = await ctx.TemplateCategories
            .AnyAsync(c => c.Name == name && c.Id != id, cancellationToken)
            .ConfigureAwait(false);

        if (nameConflict)
        {
            throw new ConflictException(
                "TemplateCategory:DuplicateName",
                $"A category with the name '{name}' already exists.");
        }

        entity.Name = name;
        entity.Description = description;
        entity.Icon = icon;
        entity.SortOrder = sortOrder;

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        int templateCount = await ctx.TemplateRevisions
            .Where(r => r.CategoryId == id)
            .Select(r => r.TemplateName)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToCategory(entity, templateCount);
    }

    /// <inheritdoc/>
    public async Task DeleteCategoryAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        TemplateCategoryEntity? entity = await ctx.TemplateCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new EntityNotFoundException(typeof(TemplateCategoryEntity), id);
        }

        int templateCount = await ctx.TemplateRevisions
            .Where(r => r.CategoryId == id)
            .Select(r => r.TemplateName)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        if (templateCount > 0)
        {
            throw new ConflictException(
                "TemplateCategory:HasTemplates",
                $"Cannot delete category with {templateCount} associated template(s).");
        }

        ctx.TemplateCategories.Remove(entity);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TemplateCategory ToCategory(TemplateCategoryEntity entity, int templateCount) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            TemplateCount = templateCount,
        };
}
