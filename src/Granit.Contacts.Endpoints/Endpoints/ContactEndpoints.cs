using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Contacts.Endpoints.Mapping;
using Granit.Guids;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.Contacts.Endpoints.Endpoints;

/// <summary>HTTP handlers for the contacts admin API. Wired by <c>ContactsEndpointRouteBuilderExtensions</c>.</summary>
internal static class ContactEndpoints
{
    public static async Task<Results<Ok<IReadOnlyList<ContactListItemResponse>>, ProblemHttpResult>> HandleListAsync(
        [FromServices] IContactReader reader,
        ContactRoles? role,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Contact> contacts = role.HasValue && role.Value != ContactRoles.None
            ? await reader.ListByRoleAsync(role.Value, cancellationToken).ConfigureAwait(false)
            : await reader.ListAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(contacts.Select(ContactMapper.ToListItem).ToList()
            as IReadOnlyList<ContactListItemResponse>);
    }

    public static async Task<Results<Ok<ContactResponse>, NotFound>> HandleGetByIdAsync(
        Guid id,
        [FromServices] IContactReader reader,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        return c is null ? TypedResults.NotFound() : TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<Created<ContactResponse>, ValidationProblem>> HandleCreateAsync(
        ContactCreateRequest request,
        [FromServices] IContactWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        var c = Contact.Create(
            guidGenerator.Create(),
            tenantId: null, // tenant filled by interceptor when in tenant context
            request.Kind,
            request.Name,
            request.DefaultCurrency,
            roles: request.Roles ?? ContactRoles.Customer,
            website: request.Website,
            language: request.Language,
            timezone: request.Timezone,
            taxId: request.TaxId,
            registrationNumber: request.RegistrationNumber);

        await writer.AddAsync(c, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/contacts/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<Ok<ContactResponse>, NotFound, ValidationProblem>> HandleUpdateAsync(
        Guid id,
        ContactUpdateRequest request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        c.UpdateContact(request.Name, request.Website, request.Language, request.Timezone);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleSuspendAsync(
        Guid id,
        ContactSuspendRequest? request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
        => await TransitionAsync(id, c => c.Suspend(request?.Reason), reader, writer, cancellationToken).ConfigureAwait(false);

    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleActivateAsync(
        Guid id,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
        => await TransitionAsync(id, c => c.Activate(), reader, writer, cancellationToken).ConfigureAwait(false);

    public static async Task<Results<NoContent, NotFound>> HandleArchiveAsync(
        Guid id,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.Archive();
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<ContactResponse>, NotFound, ValidationProblem>> HandleAddAddressAsync(
        Guid id,
        ContactAddressRequest request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        var addr = Address.Create(
            request.Line1, request.City, request.PostalCode, request.Country,
            request.CompanyName, request.Line2, request.State);

        c.AddAddress(guidGenerator.Create(), request.Kind, addr, request.IsDefault, request.Label);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/contacts/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveAddressAsync(
        Guid id,
        Guid addressId,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveAddress(addressId);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<ContactResponse>, NotFound, ValidationProblem>> HandleAddEmailAsync(
        Guid id,
        ContactEmailRequest request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.AddEmail(guidGenerator.Create(), request.Address, request.IsPrimary, request.Label);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/contacts/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveEmailAsync(
        Guid id, Guid emailId,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveEmail(emailId);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<ContactResponse>, NotFound, ValidationProblem>> HandleAddPhoneAsync(
        Guid id,
        ContactPhoneRequest request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.AddPhone(guidGenerator.Create(), request.Kind, request.Number, request.IsPrimary, request.Label);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/contacts/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemovePhoneAsync(
        Guid id, Guid phoneId,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemovePhone(phoneId);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<ContactResponse>, NotFound, ProblemHttpResult, ValidationProblem>> HandleAddExternalMappingAsync(
        Guid id,
        ContactExternalMappingRequest request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        try
        {
            c.AddExternalMapping(guidGenerator.Create(), request.ProviderName, request.ExternalId);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/contacts/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveExternalMappingAsync(
        Guid id, string providerName,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveExternalMapping(providerName);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<NoContent, NotFound, ValidationProblem>> HandleAddRoleAsync(
        Guid id,
        ContactRoleRequest request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.AddRole(request.Role);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveRoleAsync(
        Guid id, ContactRoles role,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveRole(role);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Ok<ContactResponse>, NotFound, ProblemHttpResult, ValidationProblem>> HandleSetTaxStatusAsync(
        Guid id,
        ContactTaxStatusRequest request,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        TaxStatus next;
        try
        {
            next = TaxStatus.Create(
                isExempt: request.IsExempt,
                reverseCharge: request.ReverseCharge,
                vatin: request.Vatin,
                evidenceBlobId: request.EvidenceBlobId);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        c.SetTaxStatus(next);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<Ok<ContactResponse>, NotFound>> HandleClearTaxStatusAsync(
        Guid id,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.SetTaxStatus(TaxStatus.Standard);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(c.ToResponse());
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> TransitionAsync(
        Guid id,
        Action<Contact> action,
        [FromServices] IContactReader reader,
        [FromServices] IContactWriter writer,
        CancellationToken cancellationToken)
    {
        Contact? c = await reader.GetByIdAsync(ContactId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        try
        {
            action(c);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
