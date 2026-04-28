using Granit.Dashboards.Domain;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// Write-side service for the dashboard's widget pool — add and remove widget
/// instances. Each call is one load + mutate + save round-trip and inherits
/// the multi-tenant filter from <see cref="DashboardsDbContext"/>. Widget ids
/// are allocated server-side via <see cref="IGuidGenerator"/> (callers never
/// choose them).
/// </summary>
internal sealed class DashboardWidgetService(DashboardsDbContext db, IGuidGenerator guidGenerator)
{
    public async Task<DashboardWidgetAddResult> AddWidgetAsync(
        Guid dashboardId,
        string widgetType,
        int position,
        int width,
        int height,
        string titleLocalizationKey,
        string configJson,
        string? metricName,
        string? queryName,
        string? requiredPermission,
        CancellationToken cancellationToken)
    {
        Dashboard? dashboard = await LoadAsync(dashboardId, cancellationToken).ConfigureAwait(false);
        if (dashboard is null)
        {
            return DashboardWidgetAddResult.NotFound();
        }

        WidgetInstance widget;
        try
        {
            widget = dashboard.AddWidget(
                widgetId: guidGenerator.Create(),
                widgetType: widgetType,
                position: position,
                width: width,
                height: height,
                titleLocalizationKey: titleLocalizationKey,
                configJson: configJson,
                metricName: metricName,
                queryName: queryName,
                requiredPermission: requiredPermission);
        }
        catch (ArgumentException ex)
        {
            return DashboardWidgetAddResult.Invalid(ex.Message);
        }

        // Explicit attach — the dashboard root navigation is configured via
        // HasMany().WithOne().HasForeignKey() without a back-navigation, so the
        // change tracker doesn't always pick up the new child as Added when
        // appended through the aggregate's collection field. Marking it
        // explicitly keeps SaveChanges deterministic.
        db.WidgetInstances.Add(widget);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return DashboardWidgetAddResult.Success(dashboard, widget);
    }

    public async Task<DashboardWidgetUpdateResult> UpdateWidgetAsync(
        Guid dashboardId,
        Guid widgetId,
        int position,
        int width,
        int height,
        string titleLocalizationKey,
        string configJson,
        CancellationToken cancellationToken)
    {
        Dashboard? dashboard = await LoadAsync(dashboardId, cancellationToken).ConfigureAwait(false);
        if (dashboard is null)
        {
            return DashboardWidgetUpdateResult.DashboardNotFound();
        }

        bool found;
        try
        {
            found = dashboard.UpdateWidget(widgetId, position, width, height, titleLocalizationKey, configJson);
        }
        catch (ArgumentException ex)
        {
            return DashboardWidgetUpdateResult.Invalid(ex.Message);
        }

        if (!found)
        {
            return DashboardWidgetUpdateResult.WidgetNotFound();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        WidgetInstance widget = dashboard.Widgets.Single(w => w.Id == widgetId);
        return DashboardWidgetUpdateResult.Success(widget);
    }

    public async Task<DashboardWidgetRemoveResult> RemoveWidgetAsync(
        Guid dashboardId,
        Guid widgetId,
        CancellationToken cancellationToken)
    {
        Dashboard? dashboard = await LoadAsync(dashboardId, cancellationToken).ConfigureAwait(false);
        if (dashboard is null)
        {
            return DashboardWidgetRemoveResult.DashboardNotFound;
        }

        bool wasPresent = dashboard.Widgets.Any(w => w.Id == widgetId);
        dashboard.RemoveWidget(widgetId);

        if (!wasPresent)
        {
            return DashboardWidgetRemoveResult.WidgetNotFound;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return DashboardWidgetRemoveResult.Removed;
    }

    private Task<Dashboard?> LoadAsync(Guid id, CancellationToken cancellationToken)
        => db.Dashboards
            .Include(d => d.Widgets)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
}

/// <summary>
/// Outcome of an add request — created widget bundled with its parent dashboard
/// summary, or 404 (no row in scope), or 422 (domain-guard fallback).
/// </summary>
internal sealed record DashboardWidgetAddResult(
    DashboardWidgetAddOutcome Outcome,
    Dashboard? Dashboard,
    WidgetInstance? Widget,
    string? InvalidReason)
{
    public static DashboardWidgetAddResult Success(Dashboard dashboard, WidgetInstance widget)
        => new(DashboardWidgetAddOutcome.Created, dashboard, widget, null);

    public static DashboardWidgetAddResult NotFound()
        => new(DashboardWidgetAddOutcome.NotFound, null, null, null);

    public static DashboardWidgetAddResult Invalid(string reason)
        => new(DashboardWidgetAddOutcome.Invalid, null, null, reason);
}

internal enum DashboardWidgetAddOutcome
{
    Created,
    NotFound,
    Invalid,
}

/// <summary>
/// Outcome of a remove request — distinguishes "dashboard not found" (404 on
/// the parent path) from "widget not found within an existing dashboard" (404
/// on the widget id), which the caller surfaces as different error messages.
/// </summary>
internal enum DashboardWidgetRemoveResult
{
    Removed,
    DashboardNotFound,
    WidgetNotFound,
}

/// <summary>
/// Outcome of an update request — updated widget on success, or one of three
/// failure reasons (dashboard not found, widget not found on that dashboard,
/// domain-guard rejection).
/// </summary>
internal sealed record DashboardWidgetUpdateResult(
    DashboardWidgetUpdateOutcome Outcome,
    WidgetInstance? Widget,
    string? InvalidReason)
{
    public static DashboardWidgetUpdateResult Success(WidgetInstance widget)
        => new(DashboardWidgetUpdateOutcome.Updated, widget, null);

    public static DashboardWidgetUpdateResult DashboardNotFound()
        => new(DashboardWidgetUpdateOutcome.DashboardNotFound, null, null);

    public static DashboardWidgetUpdateResult WidgetNotFound()
        => new(DashboardWidgetUpdateOutcome.WidgetNotFound, null, null);

    public static DashboardWidgetUpdateResult Invalid(string reason)
        => new(DashboardWidgetUpdateOutcome.Invalid, null, reason);
}

internal enum DashboardWidgetUpdateOutcome
{
    Updated,
    DashboardNotFound,
    WidgetNotFound,
    Invalid,
}
