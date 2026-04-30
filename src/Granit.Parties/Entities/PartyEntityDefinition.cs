using Granit.Entities;
using Granit.Parties.Domain;
using Granit.Parties.Exports;
using Granit.Parties.Metrics;
using Granit.Parties.Queries;

namespace Granit.Parties.Entities;

/// <summary>
/// Phase 1.F cobaye — declares the <see cref="Party"/> aggregate's UI surface
/// (form / detail / list collection) so the showcase host can render it without
/// any per-form scaffolding (per ADR-040).
/// </summary>
/// <remarks>
/// <para>
/// The same <c>EntityDefinition</c> is reused across workspaces — the showcase
/// CRM workspace (story #1566) and the Accounting workspace each pull in
/// <c>Party</c> with their own preset overlay; they share this definition.
/// </para>
/// <para>
/// Form variants:
/// <list type="bullet">
///   <item><c>"default"</c> — full edit form with identity, locale, contact, and metadata sections.</item>
///   <item><c>"quick"</c> — minimum-input subset for the SPA's quick-create modal (Name + Currency + Status).</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PartyEntityDefinition : EntityDefinition<Party>
{
    /// <inheritdoc />
    public override string Name => "Granit.Parties.Party";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<Party> builder) =>
        builder
            .DisplayKey("Parties:Entity.Party")
            .Icon("users")
            .PermissionGroup("Parties.Parties")
            .DisplayProperty(p => p.Name)
            .Query<PartyQueryDefinition>()
            .Export<PartyExportDefinition>()
            .Metric<PartyCountMetricDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(p => p.Name)
                    .Field(p => p.Kind)
                    .Field(p => p.Roles)
                    .Field(p => p.Status))
                .Section("locale", s => s
                    .Field(p => p.DefaultCurrency)
                    .Field(p => p.Language)
                    .Field(p => p.Timezone))
                .Section("contact", s => s
                    .Field(p => p.Website)
                    .Field(p => p.TaxId)
                    .Field(p => p.RegistrationNumber)))
            .Form("quick", f => f
                .Section("essentials", s => s
                    .Field(p => p.Name)
                    .Field(p => p.DefaultCurrency)
                    .Field(p => p.Status)))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            });
}
