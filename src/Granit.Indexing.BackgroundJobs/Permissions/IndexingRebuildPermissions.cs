namespace Granit.Indexing.BackgroundJobs.Permissions;

/// <summary>
/// Permission constants for the indexing-rebuild job. The framework module ships only
/// the canonical name — host applications grant it through their own
/// <c>IPermissionDefinitionProvider</c> and enforce it at the dispatch site before
/// calling <c>IBackgroundJobDispatcher.PublishAsync(new RebuildIndexJob&lt;TKey&gt;(...))</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why host-enforced.</b> The job runs out of band of the originating HTTP request,
/// so the framework cannot derive the principal from <c>HttpContext</c> at handler time.
/// The handler trusts the envelope; the dispatch site is the trust boundary. Without an
/// enforced permission gate, any in-process code that obtains
/// <see cref="Granit.BackgroundJobs.Abstractions.IBackgroundJobDispatcher"/> can trigger
/// a rebuild for any tenant — including the privileged <c>TenantId == null</c>
/// (cross-tenant) variant.
/// </para>
/// <para>
/// <b>Recommended grants.</b>
/// <list type="bullet">
///   <item><see cref="Rebuild.Execute"/> — single tenant rebuild; safe for tenant admins.</item>
///   <item><see cref="Rebuild.ExecuteGlobal"/> — cross-tenant (<c>TenantId == null</c>)
///         rebuild; restrict to host-side operators (<c>MultiTenancySides.Host</c>).</item>
/// </list>
/// </para>
/// </remarks>
public static class IndexingRebuildPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Indexing";

    /// <summary>Permissions for triggering the rebuild background job.</summary>
    public static class Rebuild
    {
        /// <summary>Grants tenant-scoped rebuild dispatch — the dispatcher must pass its own tenant id.</summary>
        public const string Execute = "Indexing.Rebuild.Execute";

        /// <summary>
        /// Grants cross-tenant rebuild dispatch — i.e. the ability to publish a
        /// <c>RebuildIndexJob</c> with <c>TenantId == null</c>. Restrict to host-side
        /// operators; this scans the entire dataset.
        /// </summary>
        public const string ExecuteGlobal = "Indexing.Rebuild.ExecuteGlobal";
    }
}
