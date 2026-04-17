using System.Collections.Immutable;
using System.Text.Json;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class PaymentMethodConfigurationEntityTypeConfiguration
    : IEntityTypeConfiguration<PaymentMethodConfiguration>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<PaymentMethodConfiguration> builder)
    {
        builder.ToTable(
            GranitPaymentsDbProperties.DbTablePrefix + "payment_method_configurations",
            GranitPaymentsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MethodType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        ConfigureCapabilitySnapshot(builder);

        builder.HasIndex(e => new { e.ProviderName, e.MethodType })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitPaymentsDbProperties.DbTablePrefix}method_configs_provider_method");
    }

    private static void ConfigureCapabilitySnapshot(EntityTypeBuilder<PaymentMethodConfiguration> builder)
    {
        ValueConverter<ImmutableHashSet<string>?, string?> setConverter = new(
            v => SetToCsv(v),
            v => CsvToSet(v));

        ValueComparer<ImmutableHashSet<string>?> setComparer = new(
            (l, r) => ReferenceEquals(l, r) || (l != null && r != null && l.SetEquals(r)),
            v => v == null ? 0 : v.Aggregate(0, (h, c) => HashCode.Combine(h, c)),
            v => v);

        builder.Property(e => e.SupportedCountries)
            .HasConversion(setConverter, setComparer)
            .HasMaxLength(512);

        builder.Property(e => e.SupportedCurrencies)
            .HasConversion(setConverter, setComparer)
            .HasMaxLength(256);

        builder.Property(e => e.SupportedSequenceTypes)
            .HasConversion<int?>();

        ValueConverter<ImmutableDictionary<string, PaymentMethodAmountBound>?, string?> boundsConverter = new(
            v => BoundsToJson(v),
            v => JsonToBounds(v));

        ValueComparer<ImmutableDictionary<string, PaymentMethodAmountBound>?> boundsComparer = new(
            (l, r) => ReferenceEquals(l, r) || (l != null && r != null
                && l.Count == r.Count
                && l.All(kv => r.ContainsKey(kv.Key) && kv.Value == r[kv.Key])),
            v => v == null ? 0 : v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value)),
            v => v);

        builder.Property(e => e.AmountBounds)
            .HasConversion(boundsConverter, boundsComparer);
    }

    // Empty sets / dictionaries collapse to NULL on the way to the DB — both to keep the
    // column compact and to avoid the ambiguity between "" / {} (captured wildcard) and
    // actually-empty provider declarations. The domain layer
    // (PaymentMethodConfiguration.SnapshotCapability / GetCapabilitySnapshot) owns the
    // "wildcard ↔ empty collection" semantic; these converters are purely mechanical.

    private static string? SetToCsv(ImmutableHashSet<string>? set) =>
        set is null || set.Count == 0
            ? null
            : string.Join(',', set.OrderBy(s => s, StringComparer.Ordinal));

    private static ImmutableHashSet<string>? CsvToSet(string? csv) =>
        string.IsNullOrEmpty(csv)
            ? null
            : [.. csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static string? BoundsToJson(ImmutableDictionary<string, PaymentMethodAmountBound>? bounds) =>
        bounds is null || bounds.Count == 0
            ? null
            : JsonSerializer.Serialize(bounds, JsonOptions);

    private static ImmutableDictionary<string, PaymentMethodAmountBound>? JsonToBounds(string? json) =>
        string.IsNullOrEmpty(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, PaymentMethodAmountBound>>(json, JsonOptions)
                ?.ToImmutableDictionary(StringComparer.Ordinal);
}
