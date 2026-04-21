using Granit.Payments.Endpoints.Endpoints;
using Granit.Payments.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Payments.Endpoints.Extensions;

/// <summary>Extension methods for registering payment endpoints.</summary>
public static class PaymentsEndpointRouteBuilderExtensions
{
    /// <summary>Maps the payment endpoints.</summary>
    /// <remarks>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitPayments();
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="PaymentsEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitPayments(
        this IEndpointRouteBuilder endpoints,
        Action<PaymentsEndpointsOptions>? configure = null)
    {
        PaymentsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);

        RouteGroupBuilder transactionsGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.TransactionsTagName);
        transactionsGroup.MapTransactionEndpoints();

        RouteGroupBuilder methodsGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.MethodsTagName);
        methodsGroup.MapPaymentMethodEndpoints();

        RouteGroupBuilder configurationGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.ConfigurationTagName);
        configurationGroup.MapPaymentMethodConfigurationEndpoints();

        RouteGroupBuilder webhooksGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.WebhooksTagName);
        webhooksGroup.MapWebhookEndpoints();

        return group;
    }
}
