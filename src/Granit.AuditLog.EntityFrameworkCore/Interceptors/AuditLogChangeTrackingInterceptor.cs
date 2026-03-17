using Granit.AuditLog.EntityFrameworkCore.Internal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AuditLog.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core interceptor that captures entity changes for the audit trail.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stateless by design:</b> EF Core interceptors added via <c>AddInterceptors()</c>
/// are singleton-scoped (tied to the DbContextOptions cache). All mutable state lives in
/// the scoped <see cref="ChangeTrackingCaptureService"/>, resolved from the DbContext's
/// service provider on each <c>SaveChanges</c> call.
/// </para>
/// <para>
/// <b>Two-phase capture:</b>
/// <list type="number">
///   <item><c>SavingChangesAsync</c>: snapshots ChangeTracker entries (before save).</item>
///   <item><c>SavedChangesAsync</c>: publishes the captured batch (after successful commit).</item>
/// </list>
/// </para>
/// <para>
/// Must be registered <b>after</b> all other Granit interceptors (via
/// <c>UseGranitAuditLogInterceptor</c>) so that audit fields and soft-delete
/// state are already applied when we read them.
/// </para>
/// </remarks>
public sealed class AuditLogChangeTrackingInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        PublishCapturedChanges(eventData.Context).AsTask().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PublishCapturedChanges(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private static void CaptureChanges(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        ChangeTrackingCaptureService? captureService =
            ((IInfrastructure<IServiceProvider>)context).Instance
            .GetService<ChangeTrackingCaptureService>();

        captureService?.Capture(context);
    }

    private static async ValueTask PublishCapturedChanges(
        DbContext? context,
        CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            return;
        }

        ChangeTrackingCaptureService? captureService =
            ((IInfrastructure<IServiceProvider>)context).Instance
            .GetService<ChangeTrackingCaptureService>();

        if (captureService is not null)
        {
            await captureService.PublishAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
