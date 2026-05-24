namespace Granit.Domain;

/// <summary>
/// Opt-out marker for the Granit enum persistence convention.
/// </summary>
/// <remarks>
/// <para>
/// By default, <c>ApplyGranitConventions</c> persists every enum property as its
/// PascalCase string name in a <c>varchar</c> column — see the framework
/// documentation for the rationale (readability, refactor safety, symmetry with
/// the wire format, resilience to enum reordering).
/// </para>
/// <para>
/// Apply this attribute to a property to keep the EF Core default behaviour
/// (persistence as the enum's underlying integer). Use sparingly. Valid reasons:
/// </para>
/// <list type="bullet">
///   <item><see cref="FlagsAttribute"/> bitmask enums where composition semantics require an int column.</item>
///   <item>High-write tables where the 4-byte savings per row are measurable (presence, telemetry).</item>
///   <item>Pre-existing database contracts that cannot be migrated.</item>
/// </list>
/// <para>
/// Document the reason in an XML comment above the property — future maintainers
/// need to understand why this site deviates from the convention.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PersistAsIntAttribute : Attribute;
