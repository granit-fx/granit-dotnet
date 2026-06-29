using System.Linq.Expressions;
using Granit.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Maps the framework address-enrichment value objects (<see cref="AddressGeocoding"/>,
/// <see cref="AddressVerification"/>) to flat, typed columns via EF Core's <c>ComplexProperty</c> — the
/// convention-sanctioned alternative to the default JSON serialization for multi-field value objects (see
/// <c>ApplyGranitConventions</c>). Centralizes the mapping so every address-holding entity persists these
/// the same way (status columns stay queryable for dashboards, the coordinate feeds map widgets) without
/// re-declaring the <c>ComplexProperty</c> shape per entity.
/// </summary>
/// <remarks>
/// The value-object convention's enum-as-string pass only reaches entity properties, not complex-type
/// sub-properties, so the enum conversions are applied explicitly here — this helper is the single site
/// that guarantees the framework's PascalCase-string enum persistence for these owned types.
/// </remarks>
public static class AddressEnrichmentModelBuilderExtensions
{
    // Covers the longest enum value name across the address-enrichment enums
    // ("VerificationProvider" = 20) with the convention's +4 buffer.
    private const int EnumColumnLength = 24;

    /// <summary>
    /// Maps an <see cref="AddressGeocoding"/> property to flat columns. The computed
    /// <see cref="AddressGeocoding.Coordinate"/> is ignored (the scalar latitude/longitude columns are the
    /// source of truth) and the <see cref="AddressGeocodingStatus"/> / <see cref="GeocodeMatchPrecision"/>
    /// enums are persisted as their PascalCase string names.
    /// </summary>
    /// <typeparam name="TEntity">The owning entity type.</typeparam>
    /// <param name="builder">The entity type builder.</param>
    /// <param name="selector">Selects the <see cref="AddressGeocoding"/> property to map.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static EntityTypeBuilder<TEntity> MapAddressGeocoding<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, AddressGeocoding?>> selector)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(selector);

        builder.ComplexProperty(selector, geocoding =>
        {
            geocoding.Ignore(nameof(AddressGeocoding.Coordinate));
            geocoding.Property(g => g.Status).HasConversion<string>().HasMaxLength(EnumColumnLength);
            geocoding.Property(g => g.MatchPrecision).HasConversion<string>().HasMaxLength(EnumColumnLength);
            geocoding.Property(g => g.HouseNumber).HasMaxLength(32);
            geocoding.Property(g => g.PoBox).HasMaxLength(32);
        });

        return builder;
    }

    /// <summary>
    /// Maps an <see cref="AddressVerification"/> property to flat columns, persisting the
    /// <see cref="AddressVerificationStatus"/> / <see cref="AddressVerificationSource"/> enums as their
    /// PascalCase string names.
    /// </summary>
    /// <typeparam name="TEntity">The owning entity type.</typeparam>
    /// <param name="builder">The entity type builder.</param>
    /// <param name="selector">Selects the <see cref="AddressVerification"/> property to map.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static EntityTypeBuilder<TEntity> MapAddressVerification<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, AddressVerification?>> selector)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(selector);

        builder.ComplexProperty(selector, verification =>
        {
            verification.Property(v => v.Status).HasConversion<string>().HasMaxLength(EnumColumnLength);
            verification.Property(v => v.Source).HasConversion<string>().HasMaxLength(EnumColumnLength);
            verification.Property(v => v.VerifiedBy).HasMaxLength(256);
            verification.Property(v => v.Evidence).HasMaxLength(256);
        });

        return builder;
    }
}
