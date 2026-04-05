using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Granit.Privacy.LegalAgreements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Privacy.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering EF Core persistence for Granit.Privacy.</summary>
public static class PrivacyEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for Granit.Privacy legal document management.</summary>
    public static IHostApplicationBuilder AddGranitPrivacyEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<PrivacyDbContext>(configure);
        builder.Services.AddInternalDbContextEnsurer<PrivacyDbContext>();

        builder.Services.AddScoped<EfLegalDocumentStore>();
        builder.Services.TryAddScoped<ILegalDocumentReader>(sp => sp.GetRequiredService<EfLegalDocumentStore>());
        builder.Services.TryAddScoped<ILegalDocumentWriter>(sp => sp.GetRequiredService<EfLegalDocumentStore>());

        builder.Services.TryAddScoped<ILegalDocumentPublicationService, LegalDocumentPublicationService>();

        return builder;
    }
}
