using Granit.Diagnostics;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Internal;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Internal;
using Granit.Privacy.Options;
using Granit.Privacy.ProcessingPurposes;
using Granit.Privacy.ProcessingPurposes.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.Extensions;

/// <summary>
/// Extensions for registering Granit.Privacy module services.
/// </summary>
public static class PrivacyServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit.Privacy services (IDataProviderRegistry, ILegalDocumentRegistry,
    /// ILegalAgreementChecker) and registers data providers and legal documents
    /// declared in the builder.
    /// </summary>
    public static IServiceCollection AddGranitPrivacy(
        this IServiceCollection services,
        Action<GranitPrivacyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        GranitActivitySourceRegistry.Register(PrivacyActivitySource.Name);
        services.TryAddSingleton<PrivacyMetrics>();

        services.AddOptions<GranitPrivacyOptions>()
            .BindConfiguration(GranitPrivacyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        DataProviderRegistry dataProviderRegistry = new();
        LegalDocumentRegistry legalDocumentRegistry = new();

        GranitPrivacyBuilder builder = new(services);
        configure(builder);

        foreach (string providerName in builder.DataProviderNames)
        {
            dataProviderRegistry.Register(providerName);
        }

        foreach (ProviderRegistration registration in builder.DataProviderRegistrations)
        {
            dataProviderRegistry.Register(registration);
        }

        foreach (LegalDocumentDefinition document in builder.LegalDocuments)
        {
            legalDocumentRegistry.Register(document);
        }

        ProcessingPurposeRegistry purposeRegistry = new(builder.ProcessingPurposes);

        services.TryAddSingleton<IDataProviderRegistry>(dataProviderRegistry);
        services.TryAddSingleton(legalDocumentRegistry);
        services.TryAddSingleton<IProcessingPurposeRegistry>(purposeRegistry);

        // Scope-visibility infrastructure. Default policy is fail-open (every provider that
        // has data stays visible) — hosts override via services.AddSingleton<IPrivacyScopeVisibilityPolicy, ...>.
        services.TryAddSingleton<IPrivacyScopeVisibilityPolicy, AllowAllPrivacyScopeVisibilityPolicy>();
        services.TryAddScoped<IPrivacyScopeResolver, PrivacyScopeResolver>();

        // ROPA / ISO 27001 audit trail — hosts that wire Granit.Privacy.Auditing replace this
        // with the IAuditingWriter-backed adapter via DI overrides.
        services.TryAddSingleton<IPrivacyExportAuditWriter, NullPrivacyExportAuditWriter>();

        // Backward-compatible default — accepts every subject id. Hosts exposing
        // POST /privacy/exports/on-behalf-of MUST override with a tenant-bound
        // impl (see UseUserLookupForPrivacySubjectValidation in Granit.Privacy.Endpoints).
        services.TryAddSingleton<IPrivacySubjectValidator, NullPrivacySubjectValidator>();

        // When Granit.Privacy.EntityFrameworkCore is wired (ILegalDocumentReader registered),
        // use the composite registry (DB-first, static-fallback with distributed cache).
        // Otherwise, use the static registry directly.
        bool hasDocumentReader = services.Any(d => d.ServiceType == typeof(ILegalDocumentReader));
        if (hasDocumentReader)
        {
            services.TryAddSingleton<ILegalDocumentRegistry>(sp =>
                new CompositeLegalDocumentRegistry(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<LegalDocumentRegistry>()));
        }
        else
        {
            services.TryAddSingleton<ILegalDocumentRegistry>(legalDocumentRegistry);
        }

        // Only register the checker when a store implementation has been provided
        // via UseLegalAgreementStore<T>(). Without a store the checker cannot work
        // and would cause a DI validation failure at startup.
        bool hasStore = services.Any(d => d.ServiceType == typeof(ILegalAgreementStoreReader));
        if (hasStore)
        {
            services.TryAddScoped<ILegalAgreementChecker, LegalAgreementChecker>();
        }

        return services;
    }
}
