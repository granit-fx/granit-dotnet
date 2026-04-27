using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Endpoints;
using Granit.Parties.Endpoints.Options;
using Granit.Parties.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Parties.Endpoints.Extensions;

/// <summary>Maps the contacts admin endpoints.</summary>
public static class PartiesEndpointRouteBuilderExtensions
{
    /// <summary>Maps all contacts admin routes under the configured prefix.</summary>
    public static RouteGroupBuilder MapGranitParties(this IEndpointRouteBuilder endpoints)
    {
        PartyEndpointsOptions options = endpoints.ServiceProvider
            .GetService<IOptions<PartyEndpointsOptions>>()?.Value ?? new PartyEndpointsOptions();

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // ── List & detail ─────────────────────────────────────────────
        group.MapGet("", PartyEndpoints.HandleListAsync)
            .WithName("ListContacts")
            .WithSummary("Lists contacts in the active scope, optionally filtered by role.")
            .WithDescription("Returns all contacts visible in the active scope (host or tenant). When the optional 'role' query parameter is set, only contacts whose Roles flags include the requested role are returned. Requires Parties.Parties.Read.")
            .Produces<IReadOnlyList<PartyListItemResponse>>()
            .RequireAuthorization(PartiesPermissions.Parties.Read);

        group.MapGet("/{id:guid}", PartyEndpoints.HandleGetByIdAsync)
            .WithName("GetContactById")
            .WithSummary("Returns a single contact by id.")
            .WithDescription("Returns the contact aggregate including all its addresses, emails, phones, and external mappings. Returns 404 if no contact with that id exists in the active scope.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Read);

        // ── Create / update ───────────────────────────────────────────
        group.MapPost("", PartyEndpoints.HandleCreateAsync)
            .WithName("CreateContact")
            .WithSummary("Creates a new contact in the active scope.")
            .WithDescription("Creates an Active contact in the active scope (host or tenant context). The default role set is Customer. Identity, address, email, and phone collections are populated through dedicated child endpoints after creation.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapPatch("/{id:guid}", PartyEndpoints.HandleUpdateAsync)
            .WithName("UpdateIdentity")
            .WithSummary("Updates the contact's identity (name, website, locale, timezone).")
            .WithDescription("Updates the contact's display fields. Emails / phones / addresses live in their own child collections — see the dedicated /emails, /phones, /addresses endpoints. Currency is intentionally not editable here. Returns 404 if the contact does not exist; 400 on validation failure.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Lifecycle ─────────────────────────────────────────────────
        group.MapPost("/{id:guid}/suspend", PartyEndpoints.HandleSuspendAsync)
            .WithName("SuspendContact")
            .WithSummary("Suspends a contact (idempotent; throws on Archived).")
            .WithDescription("Sets the contact's status to Suspended. The aggregate is idempotent: re-suspending an already-suspended contact is a no-op. Suspending an Archived contact returns 409 Conflict.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(PartiesPermissions.Parties.Lifecycle);

        group.MapPost("/{id:guid}/activate", PartyEndpoints.HandleActivateAsync)
            .WithName("ActivateContact")
            .WithSummary("Reactivates a suspended contact (idempotent; throws on Archived).")
            .WithDescription("Sets the contact's status back to Active. No-op if already active. Returns 409 Conflict on Archived contacts.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(PartiesPermissions.Parties.Lifecycle);

        group.MapPost("/{id:guid}/archive", PartyEndpoints.HandleArchiveAsync)
            .WithName("ArchiveContact")
            .WithSummary("Archives a contact (terminal state).")
            .WithDescription("Sets the contact's status to Archived. Archived contacts are immutable and can no longer be edited. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Lifecycle);

        // ── Addresses (multi-typed: Billing / Shipping / Other) ───────
        group.MapPost("/{id:guid}/addresses", PartyEndpoints.HandleAddAddressAsync)
            .WithName("AddContactAddress")
            .WithSummary("Adds a typed address to a contact.")
            .WithDescription("Adds a typed address. The first address of a kind is auto-promoted to default. Pass IsDefault=true to demote any existing default of the same kind.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/addresses/{addressId:guid}", PartyEndpoints.HandleRemoveAddressAsync)
            .WithName("RemoveContactAddress")
            .WithSummary("Removes an address from a contact.")
            .WithDescription("Removes an address by id. If the removed address was the default for its kind, another address of the same kind (if any) is promoted to default. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Emails ────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/emails", PartyEndpoints.HandleAddEmailAsync)
            .WithName("AddContactEmail")
            .WithSummary("Adds an email to a contact.")
            .WithDescription("Adds an email. The first email is auto-promoted to primary. Pass IsPrimary=true to demote any existing primary email.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/emails/{emailId:guid}", PartyEndpoints.HandleRemoveEmailAsync)
            .WithName("RemoveContactEmail")
            .WithSummary("Removes an email from a contact.")
            .WithDescription("Removes an email by id. If the removed email was primary, another email (if any) is promoted to primary. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Phones (typed: Mobile / Office / Home / Other) ────────────
        group.MapPost("/{id:guid}/phones", PartyEndpoints.HandleAddPhoneAsync)
            .WithName("AddContactPhone")
            .WithSummary("Adds a phone number to a contact.")
            .WithDescription("Adds a typed phone (Mobile / Office / Home / Other). The first phone is auto-promoted to primary. Pass IsPrimary=true to demote any existing primary phone.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/phones/{phoneId:guid}", PartyEndpoints.HandleRemovePhoneAsync)
            .WithName("RemoveContactPhone")
            .WithSummary("Removes a phone number from a contact.")
            .WithDescription("Removes a phone by id. If the removed phone was primary, another phone (if any) is promoted to primary. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── External mappings (one per provider) ──────────────────────
        group.MapPost("/{id:guid}/external-mappings", PartyEndpoints.HandleAddExternalMappingAsync)
            .WithName("AddContactExternalMapping")
            .WithSummary("Registers an external provider identifier (Stripe / Mollie / Odoo / …).")
            .WithDescription("Registers a polyglot external mapping. Returns 409 Conflict if a mapping for the same provider already exists — remove the existing one first.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.ExternalMappings);

        group.MapDelete("/{id:guid}/external-mappings/{providerName}", PartyEndpoints.HandleRemoveExternalMappingAsync)
            .WithName("RemoveContactExternalMapping")
            .WithSummary("Removes an external mapping.")
            .WithDescription("Removes the contact's external mapping for the given provider. Idempotent — returns 204 even if no mapping existed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.ExternalMappings);

        // ── Roles ─────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/roles", PartyEndpoints.HandleAddRoleAsync)
            .WithName("AddContactRole")
            .WithSummary("Adds a role flag to a contact.")
            .WithDescription("Adds the requested role flag(s) to the contact's Roles set. Idempotent. The standard pattern is for downstream modules to push role flags via event handlers (e.g., Invoicing adds Customer on first invoice) — this endpoint covers manual administrative overrides.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/roles/{role}", PartyEndpoints.HandleRemoveRoleAsync)
            .WithName("RemoveContactRole")
            .WithSummary("Removes a role flag from a contact.")
            .WithDescription("Removes the requested role flag from the contact's Roles set. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Tax status (customer-specific tax classification) ─────────
        group.MapPut("/{id:guid}/tax-status", PartyEndpoints.HandleSetTaxStatusAsync)
            .WithName("SetContactTaxStatus")
            .WithSummary("Sets the contact's customer-specific tax status.")
            .WithDescription("Applies VAT-exempt or B2B intra-EU reverse-charge classification to the contact. Read by Granit.Tax when computing rates: contacts with IsExempt=true or ReverseCharge=true yield 0% on every line. Reverse-charge requires a buyer-side VAT identification number. Returns 422 when the request violates a domain invariant (e.g., reverse-charge without VAT number).")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.SetTaxStatus);

        group.MapDelete("/{id:guid}/tax-status", PartyEndpoints.HandleClearTaxStatusAsync)
            .WithName("ClearContactTaxStatus")
            .WithSummary("Resets the contact's tax status to the default (no special classification).")
            .WithDescription("Clears any customer-specific tax classification. Subsequent tax calculations fall back to the country / standard rate. Idempotent.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.SetTaxStatus);

        // ── Metadata & internal notes (Stripe-style extensibility) ────
        group.MapPut("/{id:guid}/metadata", PartyEndpoints.HandleReplaceMetadataAsync)
            .WithName("ReplacePartyMetadata")
            .WithSummary("Bulk-replaces the party's metadata dictionary.")
            .WithDescription("Stripe-style customer.metadata: free-form key/value extensibility. Pass an empty object to clear. Capped at 50 entries (key ≤ 40 chars, value ≤ 500 chars). NEVER store PII here — metadata surfaces in audit logs and GDPR exports.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── vCard export (RFC 6350) ───────────────────────────────────
        group.MapGet("/{id:guid}/vcard", PartyEndpoints.HandleDownloadVCardAsync)
            .WithName("DownloadPartyVCard")
            .WithSummary("Downloads the party as a vCard 4.0 (text/vcard) file.")
            .WithDescription("Returns the party's identity, addresses, emails, phones, website, language, and timezone in vCard 4.0 format (RFC 6350). Suitable for import into address-book apps (Outlook, Apple Contacts, Google Contacts, etc.). Filename is suggested as <party-name>.vcf.")
            .Produces(StatusCodes.Status200OK, contentType: "text/vcard")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Read);

        return group;
    }
}
