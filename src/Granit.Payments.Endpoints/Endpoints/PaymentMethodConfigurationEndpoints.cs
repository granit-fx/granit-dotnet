using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Payments.Domain;
using Granit.Payments.Endpoints.Dtos;
using Granit.Payments.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Payments.Endpoints.Endpoints;

internal static class PaymentMethodConfigurationEndpoints
{
    internal static RouteGroupBuilder MapPaymentMethodConfigurationEndpoints(
        this RouteGroupBuilder group)
    {
        RouteGroupBuilder config = group.MapGroup("/configuration");

        config.MapGet("/", GetAllAsync)
            .WithName("ListPaymentMethodConfigurations")
            .WithSummary("Lists all payment method configurations.")
            .WithDescription(
                "Returns all payment method configurations for the platform, "
                + "including both active and inactive entries.")
            .Produces<IReadOnlyList<PaymentMethodConfigurationResponse>>()
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/", CreateAsync)
            .WithName("CreatePaymentMethodConfiguration")
            .WithSummary("Creates a payment method configuration (active by default).")
            .WithDescription(
                "Registers a payment method type with its provider for the platform. "
                + "The configuration is active by default. Returns 409 Conflict if a "
                + "configuration for the same provider and method type already exists.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PaymentMethodConfigurationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/{id:guid}/activate", ActivateAsync)
            .WithName("ActivatePaymentMethodConfiguration")
            .WithSummary("Activates a payment method configuration.")
            .WithDescription("Sets the configuration to active so it becomes available to tenants.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PaymentMethodConfigurationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivatePaymentMethodConfiguration")
            .WithSummary("Deactivates a payment method configuration.")
            .WithDescription("Sets the configuration to inactive. Tenants will no longer see this method.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PaymentMethodConfigurationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeletePaymentMethodConfiguration")
            .WithSummary("Deletes a payment method configuration.")
            .WithDescription("Permanently removes the configuration from the platform.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<PaymentMethodConfigurationResponse>>> GetAllAsync(
        [FromServices] IPaymentMethodConfigurationReader reader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PaymentMethodConfiguration> configs = await reader
            .GetAllAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PaymentMethodConfigurationResponse> response = configs
            .Select(MapToResponse).ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<PaymentMethodConfigurationResponse>, ProblemHttpResult>> CreateAsync(
        CreatePaymentMethodConfigurationRequest request,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        PaymentMethodConfiguration? existing = await reader
            .FindAsync(request.ProviderName, request.MethodType, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A configuration for provider '{request.ProviderName}' "
                      + $"and method type '{request.MethodType}' already exists.");
        }

        var config = PaymentMethodConfiguration.Create(
            guidGenerator.Create(),
            request.ProviderName,
            request.MethodType,
            request.DisplayLabel);

        await writer.AddAsync(config, cancellationToken).ConfigureAwait(false);

        PaymentMethodConfigurationResponse response = MapToResponse(config);
        return TypedResults.Created($"/configuration/{config.Id}", response);
    }

    private static async Task<Results<Ok<PaymentMethodConfigurationResponse>, ProblemHttpResult>> ActivateAsync(
        Guid id,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        CancellationToken cancellationToken)
    {
        PaymentMethodConfiguration? config = await reader
            .GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        config.Activate();
        await writer.UpdateAsync(config, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(MapToResponse(config));
    }

    private static async Task<Results<Ok<PaymentMethodConfigurationResponse>, ProblemHttpResult>> DeactivateAsync(
        Guid id,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        CancellationToken cancellationToken)
    {
        PaymentMethodConfiguration? config = await reader
            .GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        config.Deactivate();
        await writer.UpdateAsync(config, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(MapToResponse(config));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        CancellationToken cancellationToken)
    {
        PaymentMethodConfiguration? config = await reader
            .GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        await writer.DeleteAsync(config, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static PaymentMethodConfigurationResponse MapToResponse(PaymentMethodConfiguration c) =>
        new(c.Id, c.MethodType, c.ProviderName, c.DisplayLabel, c.Category, c.IsActive);
}
