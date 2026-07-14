namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Per-CLR-type EDM property whitelist resolved at startup by
/// <c>ValidateAndResolveWhitelists</c> and consumed by
/// <see cref="ODataEdmModelBuilder"/>. One instance exists for every entity
/// type that appears in the EDM model — EntitySet roots AND every
/// navigation-target type reachable through a whitelisted <c>$expand</c>
/// path (the transitive closure mandated by ADR-050: a type without an
/// export-derived scalar whitelist must never enter the model, or the
/// convention builder would surface all of its public properties in
/// <c>$metadata</c>).
/// </summary>
/// <param name="AllowedScalars">Scalar property names allowed on the EDM EntityType — the type's own <c>ExportDefinition.GetFields()</c> filtered to flat non-navigation paths.</param>
/// <param name="AllowedNavigations">Navigation property names allowed on the EDM EntityType — the union, across every descriptor of the mount, of the path segments that traverse this type in a whitelisted <c>$expand</c> path. Per-request path enforcement stays with the AST validator; this list only shapes what <c>$metadata</c> exposes.</param>
internal sealed record ODataEntityTypeWhitelist(
    IReadOnlyList<string> AllowedScalars,
    IReadOnlyList<string> AllowedNavigations);
