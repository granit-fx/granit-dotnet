using Granit.Entities;
using Granit.Entities.Layouts;
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
///   <item><c>"default"</c> — full edit form with identity, locale, contact, the
///     four owned-collection sections (emails, phones, addresses, external
///     mappings) and a <c>"tax"</c> read-only section for the computed tax
///     status.</item>
///   <item><c>"quick"</c> — minimum-input subset for the SPA's quick-create modal
///     (Name + Currency + Status).</item>
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
            .SubtitleProperty(p => p.Kind)
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
                    .Field(p => p.RegistrationNumber))
                .OwnedCollectionSection<PartyEmail>("emails", p => p.Emails, s => s
                    .ItemDisplayProperty(e => e.Address)
                    .ItemField(e => e.Address)
                    .ItemField(e => e.Label)
                    .ItemField(e => e.IsPrimary))
                .OwnedCollectionSection<PartyPhone>("phones", p => p.Phones, s => s
                    .ItemDisplayProperty(ph => ph.Number)
                    .ItemField(ph => ph.Kind)
                    .ItemField(ph => ph.Number)
                    .ItemField(ph => ph.Label)
                    .ItemField(ph => ph.IsPrimary))
                .OwnedCollectionSection<PartyAddress>("addresses", p => p.Addresses, s => s
                    .ItemDisplayProperty(a => a.Label)
                    .ItemField(a => a.Kind)
                    .ItemField(a => a.Label)
                    .ItemField(a => a.IsDefault))
                .OwnedCollectionSection<PartyExternalMapping>(
                    "externalMappings", p => p.ExternalMappings, s => s
                    .CollapsedByDefault()
                    .ItemDisplayProperty(m => m.ProviderName)
                    .ItemField(m => m.ProviderName)
                    .ItemField(m => m.ExternalId))
                .Section("tax", s => s
                    .Field(p => p.TaxStatus, x => x.ReadOnly())))
            .Form("quick", f => f
                .Section("essentials", s => s
                    .Field(p => p.Name)
                    .Field(p => p.DefaultCurrency)
                    .Field(p => p.Status)))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            })
            // Phase 2.A — kanban grouped by PartyStatus. The Archived column is
            // hidden by default (terminal state, ISO-27001 retention column —
            // shouldn't clutter the daily board); Suspended is collapsed.
            .KanbanView<PartyStatus>(k => k
                .GroupBy(p => p.Status)
                .Card(c => c
                    .Title(p => p.Name)
                    .Field(p => p.Kind)
                    .Field(p => p.DefaultCurrency))
                .Column(PartyStatus.Active, c => c.Color(KanbanColor.Green))
                .Column(PartyStatus.Suspended, c => c.Color(KanbanColor.Orange).Collapsed())
                .Column(PartyStatus.Archived, c => c.Color(KanbanColor.Neutral).Hidden()))
            // Phase 2.B — gallery view keyed on the avatar BlobReference.
            // Title falls back to the entity's DisplayProperty (Name);
            // subtitle pinned to PartyKind (enum) so cards distinguish
            // Persons / Companies / Departments at a glance — the renderer
            // stringifies the enum name. CardSize=Medium balances density vs
            // preview clarity for an operator-facing CRM list.
            .GalleryView(g => g
                .ImageField(p => p.Avatar)
                .SubtitleField(p => p.Kind)
                .CardSize(GalleryCardSize.Medium));
}
