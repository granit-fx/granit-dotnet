using Granit.Documents;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Documents.Notifications;

/// <summary>
/// Granit module for the Documents notification bridge — F10. Handlers subscribe to
/// share-grant / share-revoke domain events and the future quota-threshold events to
/// publish emails through <c>Granit.Notifications</c>.
/// </summary>
[DependsOn(
    typeof(GranitDocumentsModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitDocumentsNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitDocumentsNotificationsModule).Assembly);
        context.Services.AddTemplateLayout("documents.*", "Layout.Email");
    }
}
