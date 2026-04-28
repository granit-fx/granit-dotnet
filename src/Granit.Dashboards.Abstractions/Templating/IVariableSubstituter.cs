namespace Granit.Dashboards.Templating;

/// <summary>
/// Resolves <c>${variable}</c> placeholders in declarative dashboard strings (widget
/// titles, action params, filter values, ...) against a typed context. Implementations
/// MUST stay limited to literal variable lookup — no expression syntax, no scripting,
/// no piping. Anything more sophisticated belongs in the data pipeline (metric
/// definition, query) rather than a input literal.
/// </summary>
/// <remarks>
/// <para>
/// Composition order recognised by <see cref="DefaultVariableSubstituter"/> (later wins):
/// state parameters, resolved entity aliases (story P2.3), the active dashboard time
/// window, the row data when invoked from a row-click action, and the series data
/// when invoked from a chart series-click action. Consumers compose the dictionary
/// at dispatch time.
/// </para>
/// <para>
/// Unknown variables resolve to an empty string AND surface a structured log warning;
/// the substituter never throws so a typo in a input degrades visibly without
/// breaking the dashboard render.
/// </para>
/// </remarks>
public interface IVariableSubstituter
{
    /// <summary>
    /// Substitutes <c>${variable}</c> placeholders in <paramref name="input"/>
    /// against <paramref name="context"/>. Lookups are case-sensitive.
    /// </summary>
    /// <param name="input">The input string. <c>null</c> returns <c>null</c>; empty returns empty.</param>
    /// <param name="context">Variable name → value lookup. Values are formatted via
    /// <see cref="object.ToString"/>; <c>null</c> values render as empty.</param>
    /// <returns>The resolved string.</returns>
    string? Substitute(string? input, IReadOnlyDictionary<string, object?> context);
}
