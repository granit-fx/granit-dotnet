// =============================================================================
// Tests - Enum persistence convention
// =============================================================================
// Verifies that ApplyGranitConventions auto-applies EnumToStringConverter<TEnum>
// to every enum property, with the right MaxLength, except in three cases:
//   1. An explicit HasConversion<...>() in the entity configuration wins.
//   2. The property is marked [PersistAsInt] (documented opt-out).
//   3. The enum carries [Flags] (bitmask semantics, skipped automatically).
//
// All tests are model-only: they inspect IModel metadata after OnModelCreating,
// without actually persisting anything. This keeps them fast and provider-agnostic.
// =============================================================================

using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class EnumPersistenceConventionTests
{
    [Fact]
    public void Convention_AppliesEnumToStringConverter_OnStandardEnumProperty()
    {
        IProperty property = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.Status));

        ValueConverter? converter = property.GetValueConverter();
        converter.ShouldNotBeNull();
        converter.ShouldBeOfType<EnumToStringConverter<SampleStatus>>();
    }

    [Fact]
    public void Convention_SetsMaxLength_BasedOnLongestEnumValueName()
    {
        // SampleStatus values: Pending (7), Active (6), Cancelled (9) → max name = 9 → 20 (floor)
        IProperty status = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.Status));
        status.GetMaxLength().ShouldBe(20);

        // LongNameStatus longest value name → name length + 4 (growth margin)
        int longestName = Enum.GetNames<LongNameStatus>().Max(name => name.Length);
        IProperty longStatus = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.LongStatus));
        longStatus.GetMaxLength().ShouldBe(Math.Max(20, longestName + 4));
    }

    [Fact]
    public void Convention_AppliesToNullableEnumProperty()
    {
        IProperty property = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.OptionalStatus));

        ValueConverter? converter = property.GetValueConverter();
        converter.ShouldNotBeNull();
        // EF Core unwraps Nullable<T> for the converter — the same EnumToStringConverter<T> applies.
        converter.ShouldBeOfType<EnumToStringConverter<SampleStatus>>();
        property.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void Convention_SkipsProperty_MarkedPersistAsInt()
    {
        IProperty property = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.OptInIntStatus));
        property.GetValueConverter().ShouldBeNull();
        property.ClrType.ShouldBe(typeof(SampleStatus));
    }

    [Fact]
    public void Convention_SkipsFlagsEnum_ByDefault()
    {
        IProperty property = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.Permissions));
        property.GetValueConverter().ShouldBeNull();
        property.ClrType.ShouldBe(typeof(SamplePermissions));
    }

    [Fact]
    public void Convention_RespectsExplicitConversion_HasConversionString()
    {
        // Idempotent: an entity configuration that already wrote .HasConversion<string>()
        // keeps its own converter rather than being overwritten by the convention.
        IProperty property = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.ExplicitStringStatus));
        ValueConverter? converter = property.GetValueConverter();
        converter.ShouldNotBeNull();
        converter.ProviderClrType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Convention_PreservesExplicitMaxLength_OnEnumWithoutConverter()
    {
        // CustomMaxLengthStatus property has .HasMaxLength(64) set explicitly in the
        // DbContext (no HasConversion call). The convention applies its converter but
        // must NOT overwrite the explicit max length — this matters for migrated
        // configurations like AuditEntry.Category which kept 50 chars.
        IProperty property = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.CustomMaxLengthStatus));

        ValueConverter? converter = property.GetValueConverter();
        converter.ShouldNotBeNull();
        converter.ShouldBeOfType<EnumToStringConverter<SampleStatus>>();
        property.GetMaxLength().ShouldBe(64);
    }

    [Fact]
    public void Convention_DoesNotApply_ToNonEnumProperties()
    {
        IProperty intProperty = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.Counter));
        intProperty.GetValueConverter().ShouldBeNull();

        IProperty stringProperty = GetProperty<EnumTestDbContext, EnumTestEntity>(nameof(EnumTestEntity.Name));
        stringProperty.GetValueConverter().ShouldBeNull();
    }

    private static IProperty GetProperty<TContext, TEntity>(string propertyName)
        where TContext : DbContext, new()
        where TEntity : class
    {
        using TContext context = new();
        IEntityType entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} not found in model.");
        return entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found on {typeof(TEntity).Name}.");
    }
}

#pragma warning disable CA1812 // instantiated by EF Core via reflection
internal sealed class EnumTestDbContext : DbContext
{
    public DbSet<EnumTestEntity> Entities => Set<EnumTestEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseInMemoryDatabase("enum-persistence-convention-tests");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnumTestEntity>(builder =>
        {
            builder.Property(e => e.ExplicitStringStatus).HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.CustomMaxLengthStatus).HasMaxLength(64);
        });

        modelBuilder.ApplyGranitConventions();
    }
}

internal sealed class EnumTestEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Counter { get; set; }

    public SampleStatus Status { get; set; }

    public SampleStatus? OptionalStatus { get; set; }

    public LongNameStatus LongStatus { get; set; }

    [PersistAsInt]
    public SampleStatus OptInIntStatus { get; set; }

    public SamplePermissions Permissions { get; set; }

    public SampleStatus ExplicitStringStatus { get; set; }

    public SampleStatus CustomMaxLengthStatus { get; set; }
}

internal enum SampleStatus
{
    Pending,
    Active,
    Cancelled,
}

internal enum LongNameStatus
{
    Short,
    ThisIsTwentyFiveCharacters,
}

[Flags]
internal enum SamplePermissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Admin = 4,
}
#pragma warning restore CA1812
