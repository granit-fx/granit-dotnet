using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Workflow.EntityFrameworkCore;

/// <summary>
/// Base EF Core configuration for concrete <see cref="VersionedWorkflowEntity"/> entities.
/// Configures the versioning and workflow lifecycle columns common to all derived entities.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class in your application's DbContext assembly to map a concrete entity
/// that extends <see cref="VersionedWorkflowEntity"/>. Pass the target table name to the
/// base constructor. Override <see cref="ConfigureEntity"/> to add entity-specific columns
/// or indexes.
/// </para>
/// <para>
/// The table can be placed in any <c>DbContext</c> the application chooses.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// internal sealed class ContractConfiguration : VersionedWorkflowEntityBaseConfiguration&lt;Contract&gt;
/// {
///     public ContractConfiguration() : base("contracts") { }
///
///     protected override void ConfigureEntity(EntityTypeBuilder&lt;Contract&gt; builder)
///     {
///         builder.Property(e =&gt; e.Title).HasMaxLength(500).IsRequired();
///     }
/// }
/// </code>
/// </example>
/// <typeparam name="T">Concrete entity type inheriting <see cref="VersionedWorkflowEntity"/>.</typeparam>
public abstract class VersionedWorkflowEntityBaseConfiguration<T> : IEntityTypeConfiguration<T>
    where T : VersionedWorkflowEntity
{
    private readonly string _tableName;

    /// <summary>
    /// Initializes a new instance with the specified table name.
    /// </summary>
    /// <param name="tableName">
    /// The database table name (e.g., <c>"contracts"</c>). Use the module prefix convention:
    /// <c>"{module}_table_name"</c> (e.g., <c>"billing_contracts"</c>).
    /// </param>
    protected VersionedWorkflowEntityBaseConfiguration(string tableName)
    {
        _tableName = tableName;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(_tableName, GranitWorkflowDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.VersionId)
            .IsRequired();

        builder.Property(e => e.Version)
            .IsRequired();

        builder.Property(e => e.LifecycleStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.IsPublished)
            .IsRequired();

        ConfigureEntity(builder);
    }

    /// <summary>
    /// Override to add entity-specific column mappings and indexes after the base configuration.
    /// </summary>
    protected virtual void ConfigureEntity(EntityTypeBuilder<T> builder)
    {
    }
}
