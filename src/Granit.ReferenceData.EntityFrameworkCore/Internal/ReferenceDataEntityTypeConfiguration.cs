using Granit.ReferenceData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.ReferenceData.EntityFrameworkCore.Internal;

/// <summary>
/// Base EF Core Fluent API configuration for reference data entities.
/// Configures the common columns (Code, Label, IsActive, SortOrder, ValidFrom, ValidTo)
/// and the audit columns inherited from <see cref="Granit.Domain.AuditedEntity"/>.
/// </summary>
/// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
/// <remarks>
/// <para>
/// Subclass this configuration to customize the table name and add entity-specific
/// columns. Call <c>base.Configure(builder)</c> first, then add your custom mappings
/// in <see cref="ConfigureEntity"/>.
/// </para>
/// <para>
/// Example:
/// <code>
/// public sealed class CountryConfiguration : ReferenceDataEntityTypeConfiguration&lt;Country&gt;
/// {
///     public CountryConfiguration() : base("ref_countries") { }
///     protected override void ConfigureEntity(EntityTypeBuilder&lt;Country&gt; builder)
///     {
///         builder.Property(e =&gt; e.Alpha3Code).HasMaxLength(3);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class ReferenceDataEntityTypeConfiguration<TEntity>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : ReferenceDataEntity
{
    private readonly string _tableName;

    /// <summary>
    /// Initializes a new instance with the specified table name.
    /// </summary>
    /// <param name="tableName">The database table name (e.g., "ref_countries").</param>
    protected ReferenceDataEntityTypeConfiguration(string tableName)
    {
        _tableName = tableName;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(_tableName);

        builder.HasKey(e => e.Id);

        // Business key — unique
        builder.Property(e => e.Code)
               .HasMaxLength(50)
               .IsRequired();

        builder.HasIndex(e => e.Code)
               .IsUnique()
               .HasDatabaseName($"uq_{_tableName}_code");

        // Label (not mapped — virtual property)
        builder.Ignore(e => e.Label);

        // LabelEn
        builder.Property(e => e.LabelEn)
               .HasMaxLength(250)
               .IsRequired();

        // Translation labels (13 supported locales besides English)
        builder.Property(e => e.LabelFr).HasMaxLength(250);
        builder.Property(e => e.LabelNl).HasMaxLength(250);
        builder.Property(e => e.LabelDe).HasMaxLength(250);
        builder.Property(e => e.LabelEs).HasMaxLength(250);
        builder.Property(e => e.LabelIt).HasMaxLength(250);
        builder.Property(e => e.LabelPt).HasMaxLength(250);
        builder.Property(e => e.LabelZh).HasMaxLength(250);
        builder.Property(e => e.LabelJa).HasMaxLength(250);
        builder.Property(e => e.LabelPl).HasMaxLength(250);
        builder.Property(e => e.LabelTr).HasMaxLength(250);
        builder.Property(e => e.LabelKo).HasMaxLength(250);
        builder.Property(e => e.LabelSv).HasMaxLength(250);
        builder.Property(e => e.LabelCs).HasMaxLength(250);

        // IsActive — indexed for global query filter performance
        builder.HasIndex(e => e.IsActive)
               .HasDatabaseName($"ix_{_tableName}_is_active");

        // SortOrder
        builder.Property(e => e.SortOrder)
               .HasDefaultValue(0);

        // Validity period
        builder.Property(e => e.ValidFrom);
        builder.Property(e => e.ValidTo);

        // ExtraProperties — JSON property bag for application-level extensibility
        builder.Property(e => e.ExtraPropertiesJson)
               .HasColumnName("extra_properties_json");

        // ParentCode — optional self-referencing hierarchy
        builder.Property(e => e.ParentCode)
               .HasMaxLength(50);

        // ISO 27001 audit columns — populated by AuditedEntityInterceptor
        builder.Property(e => e.CreatedAt)
               .IsRequired();

        builder.Property(e => e.CreatedBy)
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(e => e.ModifiedAt);

        builder.Property(e => e.ModifiedBy)
               .HasMaxLength(256);

        ConfigureEntity(builder);
    }

    /// <summary>
    /// Override to add entity-specific column mappings after the base configuration.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    protected virtual void ConfigureEntity(EntityTypeBuilder<TEntity> builder)
    {
    }
}
