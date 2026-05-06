using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Events;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="ICategoryAssignmentService"/>. A
/// target carries at most one category — re-assigning the same target updates the
/// existing row instead of creating a duplicate (enforced by the unique index on
/// <c>(TenantId, TargetType, TargetId)</c>).
/// </summary>
internal sealed class CategoryAssignmentService(
    IDbContextFactory<TaxonomyDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILocalEventBus localEventBus,
    TaxonomyMetrics metrics) : ICategoryAssignmentService
{
    /// <inheritdoc />
    public async Task<CategoryAssignment> AssignAsync(
        Guid categoryId,
        string targetType,
        Guid targetId,
        Guid assignedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        CategoryAssignment? existing = await context.CategoryAssignments
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.TargetType == targetType && a.TargetId == targetId,
                cancellationToken)
            .ConfigureAwait(false);

        Guid? oldCategoryId = existing?.CategoryId;

        if (existing is not null)
        {
            existing.ChangeCategory(categoryId, assignedByUserId, clock.Now);
        }
        else
        {
            existing = CategoryAssignment.Create(
                guidGenerator.Create(),
                tenantId,
                categoryId,
                targetType,
                targetId,
                assignedByUserId,
                clock.Now);
            context.CategoryAssignments.Add(existing);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (oldCategoryId != categoryId)
        {
            metrics.RecordCategoryAssignmentChanged(tenantId?.ToString(), targetType);
            await localEventBus.PublishAsync(
                new CategoryAssignmentChangedEvent(
                    tenantId, targetType, targetId, oldCategoryId, categoryId),
                cancellationToken).ConfigureAwait(false);
        }

        return existing;
    }

    /// <inheritdoc />
    public async Task<bool> UnassignAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        CategoryAssignment? assignment = await context.CategoryAssignments
            .FirstOrDefaultAsync(
                a => a.TargetType == targetType && a.TargetId == targetId,
                cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            return false;
        }

        Guid removedCategoryId = assignment.CategoryId;
        Guid? tenantId = assignment.TenantId;

        context.CategoryAssignments.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        metrics.RecordCategoryAssignmentChanged(tenantId?.ToString(), targetType);
        await localEventBus.PublishAsync(
            new CategoryAssignmentChangedEvent(
                tenantId, targetType, targetId, removedCategoryId, NewCategoryId: null),
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<Category?> GetForTargetAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.CategoryAssignments
            .AsNoTracking()
            .Where(a => a.TargetType == targetType && a.TargetId == targetId)
            .Join(
                context.Categories.AsNoTracking(),
                assignment => assignment.CategoryId,
                category => category.Id,
                (_, category) => category)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RemoveAllAssignmentsAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CategoryAssignment> rows = await context.CategoryAssignments
            .Where(a => a.TargetType == targetType && a.TargetId == targetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return 0;
        }

        context.CategoryAssignments.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        foreach (CategoryAssignment row in rows)
        {
            metrics.RecordCategoryAssignmentChanged(row.TenantId?.ToString(), row.TargetType);
        }
        return rows.Count;
    }
}
