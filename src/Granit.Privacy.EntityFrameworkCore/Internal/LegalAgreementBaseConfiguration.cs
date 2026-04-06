using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Privacy.EntityFrameworkCore;

/// <summary>
/// Base EF Core configuration for concrete <see cref="LegalAgreementBase"/> entities.
/// </summary>
/// <remarks>
/// Inherit from this class in your application's DbContext assembly to map a concrete
/// <c>LegalAgreement</c> entity to the standard <c>privacy_legal_agreements</c> table.
/// Override <see cref="Configure"/> to add application-specific columns or indexes.
/// The table can be placed in any <c>DbContext</c> the application chooses.
/// </remarks>
/// <example>
/// <code>
/// internal sealed class LegalAgreementEntityTypeConfiguration
///     : LegalAgreementBaseConfiguration&lt;LegalAgreement&gt;;
/// </code>
/// </example>
/// <typeparam name="T">Concrete entity type inheriting <see cref="LegalAgreementBase"/>.</typeparam>
public abstract class LegalAgreementBaseConfiguration<T> : IEntityTypeConfiguration<T>
    where T : LegalAgreementBase
{
    /// <inheritdoc />
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(
            GranitPrivacyDbProperties.DbTablePrefix + "legal_agreements",
            GranitPrivacyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.DocumentId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Version)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.AcceptedAt)
            .IsRequired();

        builder.Property(e => e.IpAddress)
            .HasMaxLength(45);

        builder.HasIndex(e => new { e.UserId, e.DocumentId });
    }
}
