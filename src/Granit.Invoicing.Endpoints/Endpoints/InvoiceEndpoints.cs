using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.Dtos;
using Granit.Invoicing.Endpoints.Dtos;
using Granit.Invoicing.Endpoints.Permissions;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Invoicing.Endpoints.Endpoints;

internal static class InvoiceEndpoints
{
    internal static RouteGroupBuilder MapInvoiceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/invoices/{id:guid}", GetInvoiceByIdAsync)
            .WithName("GetInvoiceById")
            .WithSummary("Returns an invoice by ID.")
            .WithDescription(
                "Returns the full invoice details including line items, amounts, and status. " +
                "Returns 404 if the invoice does not exist.")
            .Produces<InvoiceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(InvoicingPermissions.Invoices.Read)
            .AllowHostAccess();

        group.MapGet("/invoices/{id:guid}/pdf", DownloadInvoicePdfAsync)
            .WithName("DownloadInvoicePdf")
            .WithSummary("Downloads the invoice as a PDF document.")
            .WithDescription(
                "Generates a PDF for the specified invoice using the configured document generator. " +
                "Returns 404 if the invoice does not exist.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(InvoicingPermissions.Invoices.Download)
            .AllowHostAccess();

        group.MapPost("/invoices", CreateInvoiceAsync)
            .WithName("CreateInvoice")
            .WithSummary("Creates a new invoice or credit note.")
            .WithDescription(
                "Creates a draft invoice or credit note for the current tenant. " +
                "Credit notes must reference a parent invoice via parentInvoiceId. " +
                "Line items can be added after creation. Requires tenant context.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<InvoiceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem()
            .RequireAuthorization(InvoicingPermissions.Invoices.Create)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Results<Ok<InvoiceResponse>, ProblemHttpResult>> GetInvoiceByIdAsync(
        Guid id,
        [FromServices] IInvoiceReader reader,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await reader
            .GetByIdAsync(InvoiceId.Create(id), cancellationToken).ConfigureAwait(false);

        return invoice is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(InvoiceResponse.FromEntity(invoice));
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> DownloadInvoicePdfAsync(
        Guid id,
        [FromServices] IInvoiceReader reader,
        [FromServices] IInvoiceDocumentGenerator documentGenerator,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await reader
            .GetByIdAsync(InvoiceId.Create(id), cancellationToken).ConfigureAwait(false);

        if (invoice is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        InvoiceDocumentResult result = await documentGenerator
            .GenerateAsync(invoice, cancellationToken).ConfigureAwait(false);

        return TypedResults.File(result.Content, result.ContentType, result.FileName);
    }

    private static async Task<Results<Created<InvoiceResponse>, ValidationProblem, ProblemHttpResult>> CreateInvoiceAsync(
        InvoiceCreateRequest request,
        [FromServices] IInvoiceWriter writer,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var invoice = Invoice.Create(
            guidGenerator.Create(),
            currentTenant.Id!.Value,
            request.DocumentType,
            request.Currency,
            request.CollectionMethod,
            request.BillingReason,
            creditNoteInfo: request.ParentInvoiceId.HasValue
                ? new CreditNoteInfo(InvoiceId.Create(request.ParentInvoiceId.Value), request.CreditNoteReason!)
                : null,
            period: request.PeriodStart.HasValue && request.PeriodEnd.HasValue
                ? new BillingPeriod(request.PeriodStart.Value, request.PeriodEnd.Value)
                : null);

        await writer.AddAsync(invoice, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/invoices/{invoice.Id}", InvoiceResponse.FromEntity(invoice));
    }
}
