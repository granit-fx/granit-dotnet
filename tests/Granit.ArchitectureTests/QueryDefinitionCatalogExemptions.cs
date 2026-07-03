namespace Granit.ArchitectureTests;

/// <summary>
/// <c>QueryDefinition&lt;T&gt;</c> types intentionally registered by hand rather than through the
/// <c>new()</c>-constrained <c>AddQueryDefinition&lt;TEntity, TDefinition&gt;()</c> helper. Consumed by
/// <c>QueryDefinitionCatalogTests</c>.
/// </summary>
/// <remarks>
/// <para>
/// A concrete definition without a public parameterless constructor cannot use the standard helper
/// (which binds <c>IQueryDefinitionDescriptor</c> for free) and must be wired by hand. Hand-wiring is
/// where the descriptor binding is easy to drop — dropping it removes the query from
/// <c>IQueryDefinitionRegistry</c> / <c>GET /catalog</c> / the dashboard-widget query picker. Adding an
/// entry here asserts the manual binding has been verified — cover it with a DI-resolution test — and
/// requires a one-line justification (inline comment).
/// </para>
/// <para>
/// The framework currently ships <b>none</b>: every framework <c>QueryDefinition&lt;T&gt;</c> has a public
/// parameterless constructor and registers through the helper. Downstream repos with parameterised
/// definitions list them here — e.g. granit-business's dynamically declared reference-data
/// <c>ReferenceDataQueryDefinition</c>, whose descriptor binding is proven by a DI-resolution test in
/// <c>Granit.ReferenceData.EntityFrameworkCore.Tests</c>.
/// </para>
/// </remarks>
internal static class QueryDefinitionCatalogExemptions
{
    public static readonly HashSet<string> Definitions = new(StringComparer.Ordinal);
}
