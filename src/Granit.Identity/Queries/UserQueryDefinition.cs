using Granit.Identity.Domain;
using Granit.QueryEngine;

namespace Granit.Identity.Queries;

/// <summary>
/// Admin-grid query surface for the canonical <see cref="User"/> aggregate
/// (ADR-051). Whitelists the columns that may be filtered / sorted /
/// projected via the QueryEngine pipeline (and, transitively, by the OData
/// feed once <see cref="UserEntityDefinition"/> wires up).
/// </summary>
/// <remarks>
/// Whitelisted columns are deliberately profile-only — never auth secrets
/// (<c>PasswordHash</c>, <c>SecurityStamp</c>, <c>ConcurrencyStamp</c>) or
/// security counters (<c>LockoutEnd</c>, <c>AccessFailedCount</c>) which
/// live on the <c>LocalIdentity</c> sibling per the ADR.
/// </remarks>
public sealed class UserQueryDefinition : QueryDefinition<User>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.UserQuery";

    /// <inheritdoc />
    protected override void Configure(QueryDefinitionBuilder<User> builder) =>
        builder
            .Column(u => u.DisplayName, c => c.Label("Name").LabelKey("Identity.Columns.DisplayName").Filterable().Sortable())
            .Column(u => u.Email, c => c.Label("Email").LabelKey("Identity.Columns.Email").Filterable().Sortable())
            .Column(u => u.FirstName, c => c.Label("First Name").LabelKey("Identity.Columns.FirstName").Filterable().Sortable())
            .Column(u => u.LastName, c => c.Label("Last Name").LabelKey("Identity.Columns.LastName").Filterable().Sortable())
            .Column(u => u.PhoneNumber, c => c.Label("Phone").LabelKey("Identity.Columns.PhoneNumber").Filterable())
            .Column(u => u.IsEnabled, c => c.Label("Enabled").LabelKey("Identity.Columns.IsEnabled").Filterable().Sortable())
            .Column(u => u.PreferredLocale, c => c.Label("Locale").LabelKey("Identity.Columns.PreferredLocale").Filterable())
            .Column(u => u.Timezone, c => c.Label("Timezone").LabelKey("Identity.Columns.Timezone").Filterable())
            .Column(u => u.TenantId, c => c.Label("Tenant").LabelKey("Identity.Columns.Tenant").Filterable().Sortable())
            .Column(u => u.CreatedAt, c => c.Label("Created At").LabelKey("Identity.Columns.CreatedAt").Sortable())
            .Column(u => u.ModifiedAt, c => c.Label("Modified At").LabelKey("Identity.Columns.ModifiedAt").Sortable())
            .GlobalSearch(u => u.DisplayName!, u => u.Email!, u => u.FirstName!, u => u.LastName!)
            .DefaultSort("-CreatedAt")
            .DefaultPageSize(25);
}
