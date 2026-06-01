using Granit.DataExchange.Export;
using Granit.Identity.Federated.Domain;

namespace Granit.Identity.Federated.Exports;

public sealed class FederatedIdentityExportDefinition : ExportDefinition<FederatedIdentity>
{
    public override string Name => "Granit.Identity.Federated.FederatedIdentityExport";

    protected override void Configure(ExportDefinitionBuilder<FederatedIdentity> builder)
    {
        builder
            .IncludeId()
            // UserId is the FK to the canonical User aggregate (ADR-051 B-step 3).
            // Without it the federated→canonical user link is lost on re-import.
            .Field(u => u.UserId)
            .Field(u => u.ExternalUserId)
            .Field(u => u.Enabled)
            .Field(u => u.LastSyncedAt, f => f.Format("O"))
            // MetadataJson holds extra IdP properties (Dictionary<string,string> serialized
            // as a JSON string). Preserved as-is for round-trip; re-hydrated from IdP on login.
            .Field(u => u.MetadataJson)
            .Field(u => u.TenantId)
            .Field(u => u.CreatedAt, f => f.Format("O"))
            .Field(u => u.CreatedBy)
            .Field(u => u.ModifiedAt, f => f.Format("O"))
            .Field(u => u.ModifiedBy);
        // Intentionally excluded: Username, Email, FirstName, LastName ([Encrypted]+[SensitiveData]),
        // EmailHash (peppered hash — security artifact, not portable). FederatedIdentity is a
        // cache entry re-hydrated from the IdP at login — PII export would expose plaintext
        // (EF interceptor decrypts on read) in a file without equivalent at-rest protection.
    }
}
