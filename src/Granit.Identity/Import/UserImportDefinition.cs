using Granit.DataExchange.Import;
using Granit.Identity.Domain;

namespace Granit.Identity.Import;

/// <summary>
/// CSV / XLSX import surface for the canonical <see cref="User"/> aggregate
/// (ADR-051). Mirror of <see cref="Exports.UserExportDefinition"/> — the same
/// profile field whitelist applies in both directions. Auth secrets, security
/// counters (<c>PasswordHash</c>, <c>LockoutEnd</c>, <c>AccessFailedCount</c>, …)
/// and lookup-hash columns (<c>EmailHash</c>, <c>PhoneNumberHash</c>) are never
/// importable — they live on <c>LocalIdentity</c> or are derived by the EF
/// lookup-hash interceptor. <c>TenantId</c> is intentionally excluded so the
/// resolution always goes through the ambient multi-tenant context.
/// </summary>
public sealed class UserImportDefinition : ImportDefinition<User>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.UserImport";

    /// <inheritdoc />
    protected override void Configure(ImportDefinitionBuilder<User> builder) =>
        builder
            .HasBusinessKey(u => u.Email)
            .Property(u => u.Email, p => p.DisplayName("Email").Required())
            .Property(u => u.DisplayName, p => p.DisplayName("Name").Required())
            .Property(u => u.FirstName, p => p.DisplayName("First Name"))
            .Property(u => u.LastName, p => p.DisplayName("Last Name"))
            .Property(u => u.PhoneNumber, p => p.DisplayName("Phone"))
            .Property(u => u.PreferredLocale, p => p.DisplayName("Locale"))
            .Property(u => u.Timezone, p => p.DisplayName("Timezone"))
            .Property(u => u.IsEnabled, p => p.DisplayName("Enabled"));
}
