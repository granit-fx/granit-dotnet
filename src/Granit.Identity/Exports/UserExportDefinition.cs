using Granit.DataExchange.Export;
using Granit.Identity.Domain;
using Granit.Identity.Queries;

namespace Granit.Identity.Exports;

/// <summary>
/// CSV / XLSX export surface for the canonical <see cref="User"/> aggregate
/// (ADR-051). Per ADR-050 this is also the field whitelist for the OData
/// EDM — any property not listed here cannot leak into the BI feed.
/// </summary>
/// <remarks>
/// Auth secrets and security counters (<c>PasswordHash</c>, <c>SecurityStamp</c>,
/// <c>ConcurrencyStamp</c>, <c>LockoutEnd</c>, <c>AccessFailedCount</c>,
/// <c>TwoFactorEnabled</c>) are NOT included — those live on
/// <c>LocalIdentity</c> and never cross the BI boundary. The fields below
/// are the "profile + security traces (Enabled flag)" set agreed on in
/// the User-data centralization analysis.
/// </remarks>
public sealed class UserExportDefinition : ExportDefinition<User>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.UserExport";

    /// <inheritdoc />
    public override string? QueryDefinitionName => new UserQueryDefinition().Name;

    /// <inheritdoc />
    protected override void Configure(ExportDefinitionBuilder<User> builder) =>
        builder
            .IncludeId()
            .Field(u => u.DisplayName, f => f.Header("Name"))
            .Field(u => u.Email, f => f.Header("Email"))
            .Field(u => u.FirstName, f => f.Header("First Name"))
            .Field(u => u.LastName, f => f.Header("Last Name"))
            .Field(u => u.PhoneNumber, f => f.Header("Phone"))
            .Field(u => u.IsEnabled, f => f.Header("Enabled"))
            .Field(u => u.PreferredLocale, f => f.Header("Locale"))
            .Field(u => u.Timezone, f => f.Header("Timezone"))
            .Field(u => u.TenantId, f => f.Header("Tenant"))
            .Field(u => u.CreatedAt, f => f.Header("Created At"))
            .Field(u => u.ModifiedAt, f => f.Header("Modified At"));
}
