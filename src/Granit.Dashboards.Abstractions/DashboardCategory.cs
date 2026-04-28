namespace Granit.Dashboards;

/// <summary>
/// Coarse grouping shipped on every <see cref="DashboardDefinition"/>. Drives the
/// catalogue ordering surfaced by <see cref="IDashboardDefinitionRegistry.GetAll()"/>
/// and the section grouping in the admin import dialog.
/// </summary>
/// <remarks>
/// Categories intentionally stay broad — the goal is "where would an admin look for
/// this dashboard", not "what schema is it under". A new category should only land
/// when an existing one becomes a junk drawer.
/// </remarks>
public enum DashboardCategory
{
    /// <summary>Default — uncategorised. Use only when the others genuinely do not fit.</summary>
    General = 0,

    /// <summary>Invoicing, payments, subscriptions, customer balance, tax.</summary>
    Finance = 1,

    /// <summary>AI usage, blob storage, background jobs, webhooks, observability.</summary>
    Operations = 2,

    /// <summary>Identity, authorization, auditing.</summary>
    Security = 3,

    /// <summary>Privacy / GDPR (export requests, erasure, retention).</summary>
    Compliance = 4,

    /// <summary>Multi-tenancy, platform-level admin (tenant lifecycle, feature flags).</summary>
    Platform = 5,

    /// <summary>IoT / real-time telemetry — sensors, alarms, video feeds.</summary>
    Iot = 6,
}
