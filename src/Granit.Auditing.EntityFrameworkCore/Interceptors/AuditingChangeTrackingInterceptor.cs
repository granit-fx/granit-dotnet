using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Auditing.EntityFrameworkCore.Interceptors;

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
/// <b>Three-phase pipeline:</b>
/// <list type="number">
///   <item><c>SavingChanges(Async)</c>: snapshots ChangeTracker entries and stages the
///   mapped <c>AuditEntry</c> — into the host context's own transaction when the host
///   model maps the audit entities (embedded mode), otherwise held for post-commit
///   standalone persistence.</item>
///   <item><c>SavedChanges(Async)</c>: completes the staged entry (metrics, or the
///   standalone save through the isolated <c>AuditingDbContext</c>).</item>
///   <item><c>SaveChangesFailed(Async)</c>: drops the staged entry and detaches the
///   embedded audit graph so a business retry on the same scope cannot double-write.</item>
/// </list>
/// </para>
/// <para>
/// Must be registered <b>after</b> all other Granit interceptors (via
/// <c>UseGranitAuditingInterceptor</c>) so that audit fields and soft-delete
/// state are already applied when we read them.
/// </para>
/// </remarks>
public sealed class AuditingChangeTrackingInterceptor : SaveChangesInterceptor, IGranitAutoInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureChangesAsync(eventData.Context).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await CaptureChangesAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        CompleteCapturedChangesAsync(eventData.Context).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await CompleteCapturedChangesAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DropCapturedChanges(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    /// <inheritdoc/>
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DropCapturedChanges(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// EF Core routes <c>DbUpdateConcurrencyException</c> through this hook, NOT through
    /// <c>SaveChangesFailed</c> — without it, the staged audit graph would survive a
    /// concurrency failure and a business retry on the same scope would double-write.
    /// </remarks>
    public override InterceptionResult ThrowingConcurrencyException(
        ConcurrencyExceptionEventData eventData,
        InterceptionResult result)
    {
        DropCapturedChanges(eventData.Context);
        return base.ThrowingConcurrencyException(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
        ConcurrencyExceptionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        DropCapturedChanges(eventData.Context);
        return base.ThrowingConcurrencyExceptionAsync(eventData, result, cancellationToken);
    }

    private static async ValueTask CaptureChangesAsync(
        DbContext? context,
        CancellationToken cancellationToken = default)
    {
        ChangeTrackingCaptureService? captureService = ResolveCaptureService(context);
        if (captureService is not null)
        {
            await captureService.CaptureAsync(context!, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask CompleteCapturedChangesAsync(
        DbContext? context,
        CancellationToken cancellationToken = default)
    {
        ChangeTrackingCaptureService? captureService = ResolveCaptureService(context);
        if (captureService is not null)
        {
            await captureService.OnSavedAsync(context!, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void DropCapturedChanges(DbContext? context) =>
        ResolveCaptureService(context)?.OnSaveFailed(context!);

    private static ChangeTrackingCaptureService? ResolveCaptureService(DbContext? context)
    {
        if (context is null)
        {
            return null;
        }

        // EF Core's internal service provider does NOT resolve application services on its
        // own: the MEDI GetService<T>() below only sees EF-internal registrations. The
        // fallback through CoreOptionsExtension.ApplicationServiceProvider is what actually
        // reaches the scoped capture service registered in the host container (wired by
        // UseApplicationServiceProvider in UseGranitInterceptors) — the same chain EF's own
        // context.GetService<T>() walks, made null-tolerant so contexts without the auditing
        // registration degrade to a no-op instead of throwing.
        IServiceProvider internalProvider = ((IInfrastructure<IServiceProvider>)context).Instance;

        return internalProvider.GetService<ChangeTrackingCaptureService>()
            ?? internalProvider.GetService<IDbContextOptions>()
                ?.FindExtension<CoreOptionsExtension>()
                ?.ApplicationServiceProvider
                ?.GetService<ChangeTrackingCaptureService>();
    }
}
