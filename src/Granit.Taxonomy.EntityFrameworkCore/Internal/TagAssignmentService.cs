using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="ITagAssignmentService"/>. Idempotent
/// assignment: a duplicate <c>(TenantId, TagId, TargetType, TargetId)</c> insert
/// returns the existing row instead of throwing.
/// </summary>
internal sealed class TagAssignmentService(
    IDbContextFactory<TaxonomyDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IClock clock,
    TaxonomyMetrics metrics) : ITagAssignmentService
{
    /// <inheritdoc />
    public async Task<(TagAssignment Assignment, bool Created)> AssignAsync(
        Guid tagId,
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

        TagAssignment? existing = await context.TagAssignments
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId &&
                     a.TagId == tagId &&
                     a.TargetType == targetType &&
                     a.TargetId == targetId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return (existing, Created: false);
        }

        var assignment = TagAssignment.Create(
            guidGenerator.Create(),
            tenantId,
            tagId,
            targetType,
            targetId,
            assignedByUserId,
            clock.Now);

        context.TagAssignments.Add(assignment);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Concurrent insert won the race — re-read the row and return idempotently.
            await using TaxonomyDbContext rereadContext = await contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            TagAssignment? winner = await rereadContext.TagAssignments
                .FirstOrDefaultAsync(
                    a => a.TenantId == tenantId &&
                         a.TagId == tagId &&
                         a.TargetType == targetType &&
                         a.TargetId == targetId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (winner is not null)
            {
                return (winner, Created: false);
            }
            throw;
        }

        metrics.RecordAssignmentCreated(tenantId?.ToString(), targetType);
        return (assignment, Created: true);
    }

    /// <inheritdoc />
    public async Task<bool> UnassignAsync(
        Guid tagId,
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TagAssignment? assignment = await context.TagAssignments
            .FirstOrDefaultAsync(
                a => a.TagId == tagId && a.TargetType == targetType && a.TargetId == targetId,
                cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            return false;
        }

        context.TagAssignments.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        metrics.RecordAssignmentDeleted(assignment.TenantId?.ToString(), assignment.TargetType);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> ListForTargetAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.TagAssignments
            .AsNoTracking()
            .Where(a => a.TargetType == targetType && a.TargetId == targetId)
            .Join(
                context.Tags.AsNoTracking(),
                assignment => assignment.TagId,
                tag => tag.Id,
                (_, tag) => tag)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
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

        List<TagAssignment> rows = await context.TagAssignments
            .Where(a => a.TargetType == targetType && a.TargetId == targetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return 0;
        }

        context.TagAssignments.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        foreach (TagAssignment row in rows)
        {
            metrics.RecordAssignmentDeleted(row.TenantId?.ToString(), row.TargetType);
        }
        return rows.Count;
    }
}
