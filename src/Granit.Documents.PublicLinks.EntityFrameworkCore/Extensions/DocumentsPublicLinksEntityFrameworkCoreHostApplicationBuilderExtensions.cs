using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Extensions;

/// <summary>Extensions for registering EF Core persistence for <c>Granit.Documents.PublicLinks</c>.</summary>
public static class DocumentsPublicLinksEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="IDocumentPublicLinkStore"/> and
    /// <see cref="IDocumentPublicLinkService"/> backed by
    /// <see cref="DocumentsPublicLinksDbContext"/>.
    /// </summary>
    /// <remarks>
    /// Should be called after <c>AddGranitDocumentsEntityFrameworkCore</c> so the
    /// parent documents <c>DbContext</c> and <see cref="IDocumentService"/>
    /// implementation are already wired.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitDocumentsPublicLinksEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<DocumentsPublicLinksDbContext>(configure);
        builder.Services.TryAddScoped<IDocumentPublicLinkStore, EfDocumentPublicLinkStore>();
        builder.Services.TryAddScoped<IDocumentPublicLinkService, DocumentPublicLinkService>();

        return builder;
    }
}
