namespace Granit.Workspaces;

/// <summary>
/// Module-supplied feature catalog contributor (per ADR-057). A
/// <see cref="IWorkspaceContributor"/> declares *where* something appears in
/// the UI; an <see cref="IFeatureProvider"/> declares *what exists* and
/// nothing else (capability + permission + route name + display hints).
/// </summary>
/// <remarks>
/// <para>
/// Provider implementations are registered in DI by the module's
/// <c>GranitModule</c> via the <c>AddFeatureProvider&lt;T&gt;()</c> extension
/// (see <c>Granit.Workspaces</c> runtime). The composer collects every
/// registered provider once at boot, runs each <see cref="DefineFeatures"/>
/// against a shared builder, and freezes the result into an immutable
/// <see cref="IFeatureCatalog"/>.
/// </para>
/// <para>
/// Provider order is irrelevant: feature names are required to be globally
/// unique, and the catalog rejects duplicates regardless of order.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// internal sealed class InvoicingFeatureProvider : IFeatureProvider
/// {
///     public void DefineFeatures(IFeatureCatalogBuilder catalog)
///     {
///         catalog.Add("invoicing.invoices.list", f => f
///             .Permission(InvoicingPermissions.Invoices.Read)
///             .RouteName("invoicing.invoices.list")
///             .DefaultIcon("receipt")
///             .DisplayKey("InvoicingEndpoints:Invoices.List"));
///     }
/// }
/// </code>
/// </example>
public interface IFeatureProvider
{
    /// <summary>
    /// Declares every feature this module exposes. Called once at boot.
    /// </summary>
    void DefineFeatures(IFeatureCatalogBuilder catalog);
}
