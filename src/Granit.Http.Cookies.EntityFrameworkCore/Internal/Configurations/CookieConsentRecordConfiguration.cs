using Granit.Http.Cookies.Domain;

namespace Granit.Http.Cookies.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="CookieConsentRecord"/>.
/// Table: <c>cookie_consent_records</c>.
/// </summary>
/// <param name="excludeFromMigrations">
/// <c>true</c> when a host context maps the table without owning its DDL
/// (exactly one context may emit the consent-table migrations).
/// </param>
internal sealed class CookieConsentRecordConfiguration(bool excludeFromMigrations = false)
    : IEntityTypeConfiguration<CookieConsentRecord>
{
    /// <summary>
    /// Category lists are persisted as a comma-delimited string: deterministic across
    /// providers (PostgreSQL, SQL Server, SQLite), readable in SQL audits (same rationale
    /// as enum-as-string), and cheap to LIKE-filter for BI. Safe because category names
    /// come from the closed snake_case vocabulary of <c>CookieCategoryNames</c> — no commas.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<string>, string> CategoriesConverter = new(
        value => string.Join(',', value),
        stored => stored.Length == 0
            ? Array.Empty<string>()
            : stored.Split(',', StringSplitOptions.RemoveEmptyEntries));

    private static readonly ValueComparer<IReadOnlyList<string>> CategoriesComparer = new(
        (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>(), StringComparer.Ordinal),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(item))),
        value => value.ToList());

    /// <summary>
    /// 5 categories x 20 chars + separators today; 512 leaves headroom for future categories.
    /// </summary>
    private const int MaxCategoriesLength = 512;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CookieConsentRecord> builder)
    {
        builder.ToTable(
            GranitHttpCookiesDbProperties.DbTablePrefix + "records",
            GranitHttpCookiesDbProperties.DbSchema,
            t => t.ExcludeFromMigrations(excludeFromMigrations));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.GrantedCategories)
            .HasConversion(CategoriesConverter, CategoriesComparer)
            .HasMaxLength(MaxCategoriesLength)
            .IsRequired();

        builder.Property(e => e.DeniedCategories)
            .HasConversion(CategoriesConverter, CategoriesComparer)
            .HasMaxLength(MaxCategoriesLength)
            .IsRequired();

        builder.Property(e => e.CmpSource)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.AnonymizedIp)
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(CookieConsentRecord.MaxUserAgentLength);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(256);

        builder.Property(e => e.DecidedAt)
            .IsRequired();

        builder.Property(e => e.TenantId);

        // Consent statistics and DPO evidence review filter on (tenant, when).
        builder.HasIndex(e => new { e.TenantId, e.DecidedAt })
            .HasDatabaseName($"ix_{GranitHttpCookiesDbProperties.DbTablePrefix}records_tenant_decided_at");

        // GDPR Art. 17 erasure scans by CreatedBy (authenticated subjects only).
        builder.HasIndex(e => e.CreatedBy)
            .HasDatabaseName($"ix_{GranitHttpCookiesDbProperties.DbTablePrefix}records_created_by");
    }
}
