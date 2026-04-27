using System.Text;
using Granit.Guids;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Internal;
using Granit.Parties.Endpoints.Mapping;
using Granit.Timing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.Parties.Endpoints.Endpoints;

/// <summary>HTTP handlers for the contacts admin API. Wired by <c>PartiesEndpointRouteBuilderExtensions</c>.</summary>
internal static class PartyEndpoints
{
    public static async Task<Results<Ok<IReadOnlyList<PartyListItemResponse>>, ProblemHttpResult>> HandleListAsync(
        [FromServices] IPartyReader reader,
        PartyRoles? role,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Party> contacts = role.HasValue && role.Value != PartyRoles.None
            ? await reader.ListByRoleAsync(role.Value, cancellationToken).ConfigureAwait(false)
            : await reader.ListAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(contacts.Select(PartyMapper.ToListItem).ToList()
            as IReadOnlyList<PartyListItemResponse>);
    }

    public static async Task<Results<Ok<PartyResponse>, NotFound>> HandleGetByIdAsync(
        Guid id,
        [FromServices] IPartyReader reader,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        return c is null ? TypedResults.NotFound() : TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<Created<PartyResponse>, Conflict<PartyCreateConflictResponse>, ValidationProblem>> HandleCreateAsync(
        PartyCreateRequest request,
        [FromQuery] bool force,
        [FromHeader(Name = "X-Skip-Duplicate-Check")] bool skipDuplicateCheck,
        [FromServices] IPartyWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IPartyDuplicateDetector detector,
        CancellationToken cancellationToken)
    {
        // Online duplicate detection (story #1302). Skipped when:
        //   ?force=true          — admin acknowledges the duplicate is intentional
        //   X-Skip-Duplicate-Check — bulk migrations / data seeding (skip the round-trip)
        if (!force && !skipDuplicateCheck)
        {
            // PartyDraft only carries fields available at create time. Emails / phones are
            // added via the dedicated POST /parties/{id}/emails endpoints later, so the
            // online check effectively covers the TaxId Tier-1 path; email / phone
            // duplicates surface through the recurring scan + per-party detail endpoint.
            PartyDraft draft = new(
                TenantId: null, // tenant filled by interceptor; detector applies the same scope
                Kind: request.Kind,
                Name: request.Name,
                TaxId: request.TaxId);

            IReadOnlyList<DuplicateCandidate> hits =
                await detector.FindCandidatesAsync(draft, cancellationToken).ConfigureAwait(false);

            // Only Tier-1 (deterministic) blocks. Tier-2 / Tier-3 are surfaced through the
            // recurring scan; blocking on a fuzzy match at create time would force admins
            // to confirm common-name overlaps every time.
            List<DuplicateCandidate> blocking = [.. hits.Where(h => h.Tier == DuplicateMatchTier.Deterministic)];
            if (blocking.Count > 0)
            {
                return TypedResults.Conflict(new PartyCreateConflictResponse(
                    Reason: "DuplicatesDetected",
                    Candidates: [.. blocking.Select(h => new PartyCreateDuplicateCandidate(
                        CandidateId: h.CandidateId.Value,
                        Score: h.Score,
                        Tier: h.Tier.ToString(),
                        Signals: [.. h.Signals.Select(s => new DuplicateMatchSignalResponse(s.Kind, s.Score))]))]));
            }
        }

        var c = Party.Create(
            guidGenerator.Create(),
            tenantId: null, // tenant filled by interceptor when in tenant context
            request.Kind,
            request.Name,
            request.DefaultCurrency,
            roles: request.Roles ?? PartyRoles.Customer,
            website: request.Website,
            language: request.Language,
            timezone: request.Timezone,
            taxId: request.TaxId,
            registrationNumber: request.RegistrationNumber,
            internalNotes: request.InternalNotes);

        await writer.AddAsync(c, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/parties/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<Ok<PartyResponse>, NotFound, ValidationProblem>> HandleUpdateAsync(
        Guid id,
        PartyUpdateRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        c.UpdateIdentity(
            request.Name,
            request.Website,
            request.Language,
            request.Timezone,
            request.InternalNotes);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleSuspendAsync(
        Guid id,
        PartySuspendRequest? request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
        => await TransitionAsync(id, c => c.Suspend(request?.Reason), reader, writer, cancellationToken).ConfigureAwait(false);

    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleActivateAsync(
        Guid id,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
        => await TransitionAsync(id, c => c.Activate(), reader, writer, cancellationToken).ConfigureAwait(false);

    public static async Task<Results<NoContent, NotFound>> HandleArchiveAsync(
        Guid id,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.Archive();
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<PartyResponse>, NotFound, ValidationProblem>> HandleAddAddressAsync(
        Guid id,
        PartyAddressRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        var addr = Address.Create(
            request.Line1, request.City, request.PostalCode, request.Country,
            request.CompanyName, request.Line2, request.State);

        c.AddAddress(guidGenerator.Create(), request.Kind, addr, request.IsDefault, request.Label);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/parties/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveAddressAsync(
        Guid id,
        Guid addressId,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveAddress(addressId);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<PartyResponse>, NotFound, ValidationProblem>> HandleAddEmailAsync(
        Guid id,
        PartyEmailRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.AddEmail(guidGenerator.Create(), request.Address, request.IsPrimary, request.Label);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/parties/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveEmailAsync(
        Guid id, Guid emailId,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveEmail(emailId);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<PartyResponse>, NotFound, ValidationProblem>> HandleAddPhoneAsync(
        Guid id,
        PartyPhoneRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.AddPhone(guidGenerator.Create(), request.Kind, request.Number, request.IsPrimary, request.Label);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/parties/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemovePhoneAsync(
        Guid id, Guid phoneId,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemovePhone(phoneId);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Created<PartyResponse>, NotFound, ProblemHttpResult, ValidationProblem>> HandleAddExternalMappingAsync(
        Guid id,
        PartyExternalMappingRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
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
        return TypedResults.Created($"/parties/{c.Id}", c.ToResponse());
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveExternalMappingAsync(
        Guid id, string providerName,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveExternalMapping(providerName);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<NoContent, NotFound, ValidationProblem>> HandleAddRoleAsync(
        Guid id,
        PartyRoleRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.AddRole(request.Role);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<NoContent, NotFound>> HandleRemoveRoleAsync(
        Guid id, PartyRoles role,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.RemoveRole(role);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Ok<PartyResponse>, NotFound, ProblemHttpResult, ValidationProblem>> HandleSetTaxStatusAsync(
        Guid id,
        PartyTaxStatusRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
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
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        c.SetTaxStatus(next);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<Ok<PartyResponse>, NotFound>> HandleClearTaxStatusAsync(
        Guid id,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }
        c.SetTaxStatus(TaxStatus.Standard);
        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<Ok<PartyResponse>, NotFound, ProblemHttpResult, ValidationProblem>> HandleReplaceMetadataAsync(
        Guid id,
        PartyMetadataRequest request,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        try
        {
            c.ReplaceMetadata(request.Metadata);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(c.ToResponse());
    }

    public static async Task<Results<FileContentHttpResult, NotFound>> HandleDownloadVCardAsync(
        Guid id,
        [FromServices] IPartyReader reader,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
        if (c is null) { return TypedResults.NotFound(); }

        string vcard = VCardBuilder.Build(c, clock.Now);
        byte[] bytes = Encoding.UTF8.GetBytes(vcard);
        return TypedResults.File(
            bytes,
            contentType: "text/vcard; charset=utf-8",
            fileDownloadName: VCardBuilder.SuggestedFileName(c));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> TransitionAsync(
        Guid id,
        Action<Party> action,
        [FromServices] IPartyReader reader,
        [FromServices] IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        Party? c = await reader.GetByIdAsync(PartyId.Create(id), cancellationToken).ConfigureAwait(false);
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
