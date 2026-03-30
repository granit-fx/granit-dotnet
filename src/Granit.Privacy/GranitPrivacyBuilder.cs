using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Internal;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Internal;
using Granit.Privacy.OptOut;
using Granit.Privacy.ProcessingPurposes;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy;

/// <summary>
/// Builder for configuring the Granit.Privacy module.
/// Used within <c>AddGranitPrivacy()</c> to register data providers, legal documents,
/// processing purposes, and opt-out tracking.
/// </summary>
public sealed class GranitPrivacyBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection.</summary>
    internal IServiceCollection Services { get; } = services;

    /// <summary>Data provider names to register at startup.</summary>
    internal List<string> DataProviderNames { get; } = [];

    /// <summary>Legal document definitions to register at startup.</summary>
    internal List<LegalDocumentDefinition> LegalDocuments { get; } = [];

    /// <summary>Processing purpose definitions to register at startup.</summary>
    internal List<ProcessingPurposeDefinition> ProcessingPurposes { get; } = [];

    /// <summary>
    /// Registers a data provider that participates in GDPR export and deletion.
    /// </summary>
    public GranitPrivacyBuilder RegisterDataProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        DataProviderNames.Add(providerName);
        return this;
    }

    /// <summary>
    /// Registers a legal document for consent versioning.
    /// </summary>
    public GranitPrivacyBuilder RegisterDocument(string documentId, string currentVersion, string displayName)
    {
        LegalDocuments.Add(new LegalDocumentDefinition(documentId, currentVersion, displayName));
        return this;
    }

    /// <summary>
    /// Registers a processing purpose with its legal basis.
    /// </summary>
    /// <param name="purposeId">Unique identifier (e.g., <c>"marketing-emails"</c>).</param>
    /// <param name="displayName">Human-readable name.</param>
    /// <param name="description">Description of the processing activity.</param>
    /// <param name="legalBasis">Legal basis code (e.g., <c>"CONSENT"</c>).</param>
    /// <param name="requiresExplicitConsent">Whether this purpose requires explicit opt-in consent.</param>
    /// <param name="dataCategory">Optional data category label.</param>
    public GranitPrivacyBuilder RegisterProcessingPurpose(
        string purposeId,
        string displayName,
        string description,
        string legalBasis,
        bool requiresExplicitConsent = false,
        string? dataCategory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purposeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(legalBasis);
        ProcessingPurposes.Add(new ProcessingPurposeDefinition(
            purposeId, displayName, description, legalBasis, requiresExplicitConsent, dataCategory));
        return this;
    }

    /// <summary>
    /// Registers the opt-out record store implementation (provided by the application).
    /// Required for CCPA "Do Not Sell or Share" functionality.
    /// </summary>
    public GranitPrivacyBuilder UseOptOutRecordStore<TStore>()
        where TStore : class, IOptOutRecordReader, IOptOutRecordWriter
    {
        Services.AddScoped<TStore>();
        Services.AddScoped<IOptOutRecordReader>(sp => sp.GetRequiredService<TStore>());
        Services.AddScoped<IOptOutRecordWriter>(sp => sp.GetRequiredService<TStore>());
        return this;
    }

    /// <summary>
    /// Registers the legal agreement store implementation (provided by the application).
    /// The concrete type is registered once, then forwarded to both
    /// <see cref="ILegalAgreementStoreReader"/> and <see cref="ILegalAgreementStoreWriter"/>.
    /// </summary>
    public GranitPrivacyBuilder UseLegalAgreementStore<TStore>()
        where TStore : class, ILegalAgreementStoreReader, ILegalAgreementStoreWriter
    {
        Services.AddScoped<TStore>();
        Services.AddScoped<ILegalAgreementStoreReader>(sp => sp.GetRequiredService<TStore>());
        Services.AddScoped<ILegalAgreementStoreWriter>(sp => sp.GetRequiredService<TStore>());
        return this;
    }

    /// <summary>
    /// Registers the deletion request tracker implementation (provided by the application).
    /// Required for deferred deletion (cooling-off period) to persist request state.
    /// </summary>
    public GranitPrivacyBuilder UseDeletionRequestTracker<TStore>()
        where TStore : class, IDeletionRequestTrackerReader, IDeletionRequestTrackerWriter
    {
        Services.AddScoped<TStore>();
        Services.AddScoped<IDeletionRequestTrackerReader>(sp => sp.GetRequiredService<TStore>());
        Services.AddScoped<IDeletionRequestTrackerWriter>(sp => sp.GetRequiredService<TStore>());
        return this;
    }

    /// <summary>
    /// Registers the export request tracker implementation (provided by the application).
    /// The concrete type is registered once, then forwarded to both
    /// <see cref="IExportRequestTrackerReader"/> and <see cref="IExportRequestTrackerWriter"/>.
    /// </summary>
    /// <remarks>
    /// Required for the <c>GET /privacy/export</c> endpoints to query export status.
    /// The application must also wire an <see cref="Events.ExportCompletedEto"/> handler
    /// that calls <see cref="IExportRequestTrackerWriter.MarkCompletedAsync"/> to update
    /// the read model when the scatter-gather saga finishes.
    /// </remarks>
    public GranitPrivacyBuilder UseExportRequestTracker<TStore>()
        where TStore : class, IExportRequestTrackerReader, IExportRequestTrackerWriter
    {
        Services.AddScoped<TStore>();
        Services.AddScoped<IExportRequestTrackerReader>(sp => sp.GetRequiredService<TStore>());
        Services.AddScoped<IExportRequestTrackerWriter>(sp => sp.GetRequiredService<TStore>());
        return this;
    }
}
