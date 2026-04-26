using Granit.Contacts.Domain;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Contacts.Endpoints.Endpoints;
using Granit.Contacts.Endpoints.Options;
using Granit.Contacts.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Contacts.Endpoints.Extensions;

/// <summary>Maps the contacts admin endpoints.</summary>
public static class ContactsEndpointRouteBuilderExtensions
{
    /// <summary>Maps all contacts admin routes under the configured prefix.</summary>
    public static RouteGroupBuilder MapGranitContacts(this IEndpointRouteBuilder endpoints)
    {
        ContactEndpointsOptions options = endpoints.ServiceProvider
            .GetService<IOptions<ContactEndpointsOptions>>()?.Value ?? new ContactEndpointsOptions();

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // ── List & detail ─────────────────────────────────────────────
        group.MapGet("", ContactEndpoints.HandleListAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Read)
            .WithName("ListContacts")
            .WithSummary("Lists contacts in the active scope, optionally filtered by role.")
            .WithDescription("Returns all contacts visible in the active scope (host or tenant). When the optional 'role' query parameter is set, only contacts whose Roles flags include the requested role are returned. Requires Contacts.Contacts.Read.")
            .Produces<IReadOnlyList<ContactListItemResponse>>();

        group.MapGet("/{id:guid}", ContactEndpoints.HandleGetByIdAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Read)
            .WithName("GetContactById")
            .WithSummary("Returns a single contact by id.")
            .WithDescription("Returns the contact aggregate including all its addresses, emails, phones, and external mappings. Returns 404 if no contact with that id exists in the active scope.")
            .Produces<ContactResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Create / update ───────────────────────────────────────────
        group.MapPost("", ContactEndpoints.HandleCreateAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("CreateContact")
            .WithSummary("Creates a new contact in the active scope.")
            .WithDescription("Creates an Active contact in the active scope (host or tenant context). The default role set is Customer. Identity, address, email, and phone collections are populated through dedicated child endpoints after creation.")
            .Produces<ContactResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPatch("/{id:guid}", ContactEndpoints.HandleUpdateAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("UpdateContact")
            .WithSummary("Updates the contact's identity (name, website, locale, timezone).")
            .WithDescription("Updates the contact's display fields. Emails / phones / addresses live in their own child collections — see the dedicated /emails, /phones, /addresses endpoints. Currency is intentionally not editable here. Returns 404 if the contact does not exist; 400 on validation failure.")
            .Produces<ContactResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        // ── Lifecycle ─────────────────────────────────────────────────
        group.MapPost("/{id:guid}/suspend", ContactEndpoints.HandleSuspendAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("SuspendContact")
            .WithSummary("Suspends a contact (idempotent; throws on Archived).")
            .WithDescription("Sets the contact's status to Suspended. The aggregate is idempotent: re-suspending an already-suspended contact is a no-op. Suspending an Archived contact returns 409 Conflict.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/activate", ContactEndpoints.HandleActivateAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("ActivateContact")
            .WithSummary("Reactivates a suspended contact (idempotent; throws on Archived).")
            .WithDescription("Sets the contact's status back to Active. No-op if already active. Returns 409 Conflict on Archived contacts.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/archive", ContactEndpoints.HandleArchiveAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("ArchiveContact")
            .WithSummary("Archives a contact (terminal state).")
            .WithDescription("Sets the contact's status to Archived. Archived contacts are immutable and can no longer be edited. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Addresses (multi-typed: Billing / Shipping / Other) ───────
        group.MapPost("/{id:guid}/addresses", ContactEndpoints.HandleAddAddressAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("AddContactAddress")
            .WithSummary("Adds a typed address to a contact.")
            .WithDescription("Adds a typed address. The first address of a kind is auto-promoted to default. Pass IsDefault=true to demote any existing default of the same kind.")
            .Produces<ContactResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}/addresses/{addressId:guid}", ContactEndpoints.HandleRemoveAddressAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("RemoveContactAddress")
            .WithSummary("Removes an address from a contact.")
            .WithDescription("Removes an address by id. If the removed address was the default for its kind, another address of the same kind (if any) is promoted to default. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Emails ────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/emails", ContactEndpoints.HandleAddEmailAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("AddContactEmail")
            .WithSummary("Adds an email to a contact.")
            .WithDescription("Adds an email. The first email is auto-promoted to primary. Pass IsPrimary=true to demote any existing primary email.")
            .Produces<ContactResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}/emails/{emailId:guid}", ContactEndpoints.HandleRemoveEmailAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("RemoveContactEmail")
            .WithSummary("Removes an email from a contact.")
            .WithDescription("Removes an email by id. If the removed email was primary, another email (if any) is promoted to primary. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Phones (typed: Mobile / Office / Home / Other) ────────────
        group.MapPost("/{id:guid}/phones", ContactEndpoints.HandleAddPhoneAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("AddContactPhone")
            .WithSummary("Adds a phone number to a contact.")
            .WithDescription("Adds a typed phone (Mobile / Office / Home / Other). The first phone is auto-promoted to primary. Pass IsPrimary=true to demote any existing primary phone.")
            .Produces<ContactResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}/phones/{phoneId:guid}", ContactEndpoints.HandleRemovePhoneAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("RemoveContactPhone")
            .WithSummary("Removes a phone number from a contact.")
            .WithDescription("Removes a phone by id. If the removed phone was primary, another phone (if any) is promoted to primary. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── External mappings (one per provider) ──────────────────────
        group.MapPost("/{id:guid}/external-mappings", ContactEndpoints.HandleAddExternalMappingAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("AddContactExternalMapping")
            .WithSummary("Registers an external provider identifier (Stripe / Mollie / Odoo / …).")
            .WithDescription("Registers a polyglot external mapping. Returns 409 Conflict if a mapping for the same provider already exists — remove the existing one first.")
            .Produces<ContactResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}/external-mappings/{providerName}", ContactEndpoints.HandleRemoveExternalMappingAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("RemoveContactExternalMapping")
            .WithSummary("Removes an external mapping.")
            .WithDescription("Removes the contact's external mapping for the given provider. Idempotent — returns 204 even if no mapping existed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Roles ─────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/roles", ContactEndpoints.HandleAddRoleAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("AddContactRole")
            .WithSummary("Adds a role flag to a contact.")
            .WithDescription("Adds the requested role flag(s) to the contact's Roles set. Idempotent. The standard pattern is for downstream modules to push role flags via event handlers (e.g., Invoicing adds Customer on first invoice) — this endpoint covers manual administrative overrides.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}/roles/{role}", ContactEndpoints.HandleRemoveRoleAsync)
            .RequireAuthorization(ContactsPermissions.Contacts.Manage)
            .WithName("RemoveContactRole")
            .WithSummary("Removes a role flag from a contact.")
            .WithDescription("Removes the requested role flag from the contact's Roles set. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
