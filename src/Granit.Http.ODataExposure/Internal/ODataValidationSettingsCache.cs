using Microsoft.AspNetCore.OData.Query.Validator;

namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Per-route single-assignment cache for the <see cref="ODataValidationSettings"/> computed
/// from the EntitySet descriptor and the query definition's metadata. One instance is captured
/// in each mapped route's closure; the settings are computed on the first request because
/// <c>IQueryEngine&lt;TEntity&gt;.GetMetadata()</c> needs a scoped engine instance, but the
/// result is derived exclusively from immutable QueryDefinition metadata, so it never changes.
/// </summary>
internal sealed class ODataValidationSettingsCache
{
    // volatile: settings are published once and read from many request threads. A duplicate
    // computation under a benign first-request race is fine — both factories produce identical
    // values from the same immutable metadata; the reference assignment is atomic.
    private volatile ODataValidationSettings? _settings;

    /// <summary>Returns the cached settings, computing them via <paramref name="factory"/> on first use.</summary>
    /// <param name="factory">Factory deriving the settings from immutable QueryDefinition metadata.</param>
    public ODataValidationSettings GetOrCreate(Func<ODataValidationSettings> factory) =>
        _settings ??= factory();
}
