using Granit.Events;
using Granit.Guids;
using Granit.Http.Cookies;
using Granit.Http.RateLimiting.AspNetCore;
using Granit.MultiTenancy;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Internal;
using Granit.Privacy.Endpoints.Options;
using Granit.Privacy.OptOut;
using Granit.Privacy.OptOut.Events;
using Granit.Privacy.Regulations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Endpoints.Endpoints;

internal static class PrivacyOptOutEndpoints
{
    internal static RouteGroupBuilder MapPrivacyOptOutEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/opt-out", HandleOptOutAsync)
             .AllowAnonymous()
             .RequireGranitRateLimiting(PrivacyOptOutRateLimitPolicies.OptOutCreate)
             .WithName("RequestOptOut")
             .WithSummary("Opts out of data sale/sharing (CCPA).")
             .WithDescription(
                 "Records a 'Do Not Sell or Share My Personal Information' request. "
                 + "Supports both authenticated users and anonymous visitors (CCPA compliance). "
                 + "For anonymous visitors, a _optout_id HTTP-Only cookie is set to track the opt-out. "
                 + "Anonymous opt-outs are stored tenant-less by default (PrivacyEndpointsOptions."
                 + nameof(PrivacyEndpointsOptions.BindAnonymousOptOutToCurrentTenant) + ") to prevent "
                 + "tenant injection via a spoofable resolver. Rate-limited via the "
                 + "`privacy-optout-create` policy.")
             .Produces<PrivacyOptOutStatusResponse>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status429TooManyRequests)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/opt-out/status", HandleGetOptOutStatusAsync)
             .AllowAnonymous()
             .WithName("GetOptOutStatus")
             .WithSummary("Returns the current opt-out status.")
             .WithDescription(
                 "Checks whether the requesting user or visitor has an active opt-out. "
                 + "For authenticated users, checks by UserId. For anonymous visitors, checks the _optout_id cookie.")
             .Produces<PrivacyOptOutStatusResponse>();

        return group;
    }

    private static async Task<Results<Created<PrivacyOptOutStatusResponse>, ProblemHttpResult>> HandleOptOutAsync(
        HttpContext httpContext,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IOptOutRecordReader? optOutReader,
        [FromServices] IOptOutRecordWriter? optOutWriter,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] TimeProvider timeProvider,
        [FromServices] PrivacyMetrics metrics,
        [FromServices] IGranitCookieManager cookieManager,
        [FromServices] IOptions<PrivacyEndpointsOptions> endpointOptions,
        CancellationToken cancellationToken)
    {
        if (optOutWriter is null)
        {
            return TypedResults.Problem(
                detail: "Opt-out functionality requires an IOptOutRecordStore registration via UseOptOutRecordStore<T>().",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        Guid recordId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        string regulation = await PrivacyResponseMapper.ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

        // Determine identity — authenticated user or anonymous visitor
        Guid? userId = PrivacyResponseMapper.TryGetUserIdOrNull(httpContext);
        string? anonymousTrackId = null;

        if (userId is null)
        {
            // Anonymous: read existing cookie or generate new tracking ID
            anonymousTrackId = httpContext.Request.Cookies[OptOutConstants.CookieName]
                ?? guidGenerator.Create().ToString();

            await cookieManager.SetCookieAsync(httpContext, OptOutConstants.CookieName, anonymousTrackId)
                .ConfigureAwait(false);
        }

        // Anonymous flows must not trust ICurrentTenant by default — many hosts resolve
        // tenancy from an attacker-controllable header (X-Tenant-Id) for cross-cutting
        // routes, which would let an unauthenticated client poison an arbitrary tenant's
        // opt-out store. Authenticated calls keep their tenant context.
        bool trustsAnonymousTenant = endpointOptions.Value.BindAnonymousOptOutToCurrentTenant;
        Guid? tenantId = userId is not null || trustsAnonymousTenant
            ? PrivacyResponseMapper.ResolveTenantId(currentTenant)
            : null;

        // Idempotency: return existing opt-out if already active
        if (optOutReader is not null)
        {
            bool alreadyOptedOut = await optOutReader.IsOptedOutAsync(userId, anonymousTrackId, cancellationToken)
                .ConfigureAwait(false);
            if (alreadyOptedOut)
            {
                return TypedResults.Created(
                    (string?)null,
                    new PrivacyOptOutStatusResponse(true, null, regulation));
            }
        }

        OptOutRecord record = new(recordId, userId, anonymousTrackId, OptOutState.Active, now, null, tenantId, regulation);
        await optOutWriter.RecordOptOutAsync(record, cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new OptOutRequestedEto(recordId, userId, anonymousTrackId, now, regulation, tenantId),
            cancellationToken).ConfigureAwait(false);

        metrics.RecordOptOutRequested(tenantId, regulation);

        return TypedResults.Created(
            (string?)null,
            new PrivacyOptOutStatusResponse(true, now, regulation));
    }

    private static async Task<Ok<PrivacyOptOutStatusResponse>> HandleGetOptOutStatusAsync(
        HttpContext httpContext,
        [FromServices] IOptOutRecordReader? optOutReader,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        CancellationToken cancellationToken)
    {
        if (optOutReader is null)
        {
            return TypedResults.Ok(new PrivacyOptOutStatusResponse(false, null, null));
        }

        Guid? userId = PrivacyResponseMapper.TryGetUserIdOrNull(httpContext);
        string? anonymousTrackId = httpContext.Request.Cookies[OptOutConstants.CookieName];

        bool isOptedOut = await optOutReader.IsOptedOutAsync(userId, anonymousTrackId, cancellationToken).ConfigureAwait(false);

        string? regulation = null;
        if (isOptedOut)
        {
            regulation = await PrivacyResponseMapper.ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);
        }

        return TypedResults.Ok(new PrivacyOptOutStatusResponse(isOptedOut, null, regulation));
    }

}
