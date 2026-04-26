using Granit.Contacts.Domain;
using Granit.DataExchange.Export;

namespace Granit.Contacts.Exports;

/// <summary>CSV / XLSX export whitelist for contacts.</summary>
public sealed class ContactExportDefinition : ExportDefinition<Contact>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Contacts.ContactExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<Contact> builder)
    {
        builder
            .IncludeId()
            .Field(c => c.Kind)
            .Field(c => c.Name)
            .Field(c => c.DefaultCurrency)
            .Field(c => c.Timezone)
            .Field(c => c.Language)
            .Field(c => c.Website)
            .Field(c => c.TaxId)
            .Field(c => c.RegistrationNumber)
            .Field(c => c.Roles)
            .Field(c => c.Status)
            .Field(c => c.CreatedAt, f => f.Format("O"))
            .Field(c => c.CreatedBy)
            .Field(c => c.ModifiedAt, f => f.Format("O"))
            .Field(c => c.ModifiedBy);
    }
}
