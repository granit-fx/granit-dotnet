using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Endpoints;
using Granit.Parties.Endpoints.Options;
using Granit.Parties.Endpoints.Permissions;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Entities;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Parties.Endpoints.Extensions;

/// <summary>Maps the parties admin endpoints.</summary>
public static class PartiesEndpointRouteBuilderExtensions
{
    /// <summary>Maps all parties admin routes under the configured prefix.</summary>
    public static RouteGroupBuilder MapGranitParties(this IEndpointRouteBuilder endpoints)
    {
        PartyEndpointsOptions options = endpoints.ServiceProvider
            .GetService<IOptions<PartyEndpointsOptions>>()?.Value ?? new PartyEndpointsOptions();

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // ── List & detail ─────────────────────────────────────────────
        group.MapGet("", PartyEndpoints.HandleListAsync)
            .WithName("ListParties")
            .WithSummary("Lists parties in the active scope, optionally filtered by role.")
            .WithDescription("Returns all parties visible in the active scope (host or tenant). When the optional 'role' query parameter is set, only parties whose Roles flags include the requested role are returned. Requires Parties.Parties.Read.")
            .Produces<IReadOnlyList<PartyListItemResponse>>()
            .RequireAuthorization(PartiesPermissions.Parties.Read);

        group.MapGet("/{id:guid}", PartyEndpoints.HandleGetByIdAsync)
            .WithName("GetPartyById")
            .WithSummary("Returns a single party by id.")
            .WithDescription("Returns the party aggregate including all its addresses, emails, phones, and external mappings. Returns 404 if no party with that id exists in the active scope.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Read);

        // ── Create / update ───────────────────────────────────────────
        group.MapPost("", PartyEndpoints.HandleCreateAsync)
            .WithName("CreateParty")
            .WithSummary("Creates a new party in the active scope.")
            .WithDescription("Creates an Active party in the active scope (host or tenant context). The default role set is Customer. Identity, address, email, and phone collections are populated through dedicated child endpoints after creation. Online duplicate detection (#1302) intercepts before INSERT — a Tier-1 deterministic match (TaxId at create time) returns 409 with the candidates list, letting the admin choose to merge or to confirm-create with ?force=true. Bulk migrations bypass the check via the X-Skip-Duplicate-Check: true header. Tier-2 / Tier-3 fuzzy matches do NOT block at create time — they surface via the recurring scan + the duplicate-candidates inbox.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .Produces<PartyCreateConflictResponse>(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapPatch("/{id:guid}", PartyEndpoints.HandleUpdateAsync)
            .WithName("UpdateIdentity")
            .WithSummary("Updates the party's identity (name, website, locale, timezone).")
            .WithDescription("Updates the party's display fields. Emails / phones / addresses live in their own child collections — see the dedicated /emails, /phones, /addresses endpoints. Currency is intentionally not editable here. Returns 404 if the party does not exist; 400 on validation failure.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Lifecycle ─────────────────────────────────────────────────
        group.MapPost("/{id:guid}/suspend", PartyEndpoints.HandleSuspendAsync)
            .WithName("SuspendParty")
            .WithSummary("Suspends a party (idempotent; throws on Archived).")
            .WithDescription("Sets the party's status to Suspended. The aggregate is idempotent: re-suspending an already-suspended party is a no-op. Suspending an Archived party returns 409 Conflict.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(PartiesPermissions.Parties.Lifecycle);

        group.MapPost("/{id:guid}/activate", PartyEndpoints.HandleActivateAsync)
            .WithName("ActivateParty")
            .WithSummary("Reactivates a suspended party (idempotent; throws on Archived).")
            .WithDescription("Sets the party's status back to Active. No-op if already active. Returns 409 Conflict on Archived parties.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(PartiesPermissions.Parties.Lifecycle);

        group.MapPost("/{id:guid}/archive", PartyEndpoints.HandleArchiveAsync)
            .WithName("ArchiveParty")
            .WithSummary("Archives a party (terminal state).")
            .WithDescription("Sets the party's status to Archived. Archived parties are immutable and can no longer be edited. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Lifecycle);

        // ── Addresses (multi-typed: Billing / Shipping / Other) ───────
        group.MapPost("/{id:guid}/addresses", PartyEndpoints.HandleAddAddressAsync)
            .WithName("AddPartyAddress")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Adds a typed address to a party.")
            .WithDescription("Adds a typed address. The first address of a kind is auto-promoted to default. Pass IsDefault=true to demote any existing default of the same kind.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/addresses/{addressId:guid}", PartyEndpoints.HandleRemoveAddressAsync)
            .WithName("RemovePartyAddress")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Removes an address from a party.")
            .WithDescription("Removes an address by id. If the removed address was the default for its kind, another address of the same kind (if any) is promoted to default. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Emails ────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/emails", PartyEndpoints.HandleAddEmailAsync)
            .WithName("AddPartyEmail")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Adds an email to a party.")
            .WithDescription("Adds an email. The first email is auto-promoted to primary. Pass IsPrimary=true to demote any existing primary email.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/emails/{emailId:guid}", PartyEndpoints.HandleRemoveEmailAsync)
            .WithName("RemovePartyEmail")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Removes an email from a party.")
            .WithDescription("Removes an email by id. If the removed email was primary, another email (if any) is promoted to primary. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Phones (typed: Mobile / Office / Home / Other) ────────────
        group.MapPost("/{id:guid}/phones", PartyEndpoints.HandleAddPhoneAsync)
            .WithName("AddPartyPhone")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Adds a phone number to a party.")
            .WithDescription("Adds a typed phone (Mobile / Office / Home / Other). The first phone is auto-promoted to primary. Pass IsPrimary=true to demote any existing primary phone.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/phones/{phoneId:guid}", PartyEndpoints.HandleRemovePhoneAsync)
            .WithName("RemovePartyPhone")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Removes a phone number from a party.")
            .WithDescription("Removes a phone by id. If the removed phone was primary, another phone (if any) is promoted to primary. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── External mappings (one per provider) ──────────────────────
        group.MapPost("/{id:guid}/external-mappings", PartyEndpoints.HandleAddExternalMappingAsync)
            .WithName("AddPartyExternalMapping")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Registers an external provider identifier (Stripe / Mollie / Odoo / …).")
            .WithDescription("Registers a polyglot external mapping. Returns 409 Conflict if a mapping for the same provider already exists — remove the existing one first.")
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.ExternalMappings);

        group.MapDelete("/{id:guid}/external-mappings/{providerName}", PartyEndpoints.HandleRemoveExternalMappingAsync)
            .WithName("RemovePartyExternalMapping")
            .WithTags(options.ContactInfoTagName)
            .WithSummary("Removes an external mapping.")
            .WithDescription("Removes the party's external mapping for the given provider. Idempotent — returns 204 even if no mapping existed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.ExternalMappings);

        // ── Roles ─────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/roles", PartyEndpoints.HandleAddRoleAsync)
            .WithName("AddPartyRole")
            .WithTags(options.ClassificationTagName)
            .WithSummary("Adds a role flag to a party.")
            .WithDescription("Adds the requested role flag(s) to the party's Roles set. Idempotent. The standard pattern is for downstream modules to push role flags via event handlers (e.g., Invoicing adds Customer on first invoice) — this endpoint covers manual administrative overrides.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        group.MapDelete("/{id:guid}/roles/{role}", PartyEndpoints.HandleRemoveRoleAsync)
            .WithName("RemovePartyRole")
            .WithTags(options.ClassificationTagName)
            .WithSummary("Removes a role flag from a party.")
            .WithDescription("Removes the requested role flag from the party's Roles set. Idempotent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Tax status (customer-specific tax classification) ─────────
        group.MapPut("/{id:guid}/tax-status", PartyEndpoints.HandleSetTaxStatusAsync)
            .WithName("SetPartyTaxStatus")
            .WithTags(options.ClassificationTagName)
            .WithSummary("Sets the party's customer-specific tax status.")
            .WithDescription("Applies VAT-exempt or B2B intra-EU reverse-charge classification to the party. Read by Granit.Tax when computing rates: parties with IsExempt=true or ReverseCharge=true yield 0% on every line. Reverse-charge requires a buyer-side VAT identification number. Returns 422 when the request violates a domain invariant (e.g., reverse-charge without VAT number).")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.SetTaxStatus);

        group.MapDelete("/{id:guid}/tax-status", PartyEndpoints.HandleClearTaxStatusAsync)
            .WithName("ClearPartyTaxStatus")
            .WithTags(options.ClassificationTagName)
            .WithSummary("Resets the party's tax status to the default (no special classification).")
            .WithDescription("Clears any customer-specific tax classification. Subsequent tax calculations fall back to the country / standard rate. Idempotent.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.SetTaxStatus);

        // ── Metadata & internal notes (Stripe-style extensibility) ────
        group.MapPut("/{id:guid}/metadata", PartyEndpoints.HandleReplaceMetadataAsync)
            .WithName("ReplacePartyMetadata")
            .WithTags(options.ClassificationTagName)
            .WithSummary("Bulk-replaces the party's metadata dictionary.")
            .WithDescription("Stripe-style customer.metadata: free-form key/value extensibility. Pass an empty object to clear. Capped at 50 entries (key ≤ 40 chars, value ≤ 500 chars). NEVER store PII here — metadata surfaces in audit logs and GDPR exports.")
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        // ── Merge (admin tool: survivor + loser → tombstoned loser) ───
        group.MapGet("/{survivorId:guid}/merge/preview", PartyMergeEndpoints.HandlePreviewAsync)
            .WithName("PreviewPartyMerge")
            .WithTags(options.MergeTagName)
            .WithSummary("Previews a party merge in dry-run mode.")
            .WithDescription("Computes per-field conflicts (with the recommended winner pre-populated) and per-rewriter row counts (Invoice.PartyId, Subscription.PartyId, BalanceAccount.PartyId, ...) without committing anything. Powers the admin merge wizard's side-by-side view. Returns 422 when a hard invariant is violated (tenant / kind / currency mismatch, archived loser).")
            .Produces<PartyMergeResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(PartiesPermissions.Parties.Merge);

        group.MapPost("/{survivorId:guid}/merge", PartyMergeEndpoints.HandleMergeAsync)
            .WithName("MergeParty")
            .WithTags(options.MergeTagName)
            .WithSummary("Merges a loser party into the survivor.")
            .WithDescription("Survivor absorbs the loser: scalar fields resolved per the supplied choices (defaults applied to missing keys), child collections rewritten via SQL bulk-update, cross-module references (Invoice.PartyId, Subscription.PartyId, BalanceAccount.PartyId) redirected onto the survivor, loser tombstoned with MergedIntoId pointing at the survivor. Atomic — any failure rolls the whole transaction back. Stripe-style idempotency via the optional Idempotency-Key header (24h replay window). Returns 422 on invariant violation, 409 on concurrent merge or idempotency-key conflict, 404 when survivor or loser does not exist.")
            .Produces<PartyMergeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Merge);

        // ── Duplicate-candidates review (Epic 2: #1301) ───────────────
        // QueryEngine listing + meta + saved-views, plus the dedicated dismiss / merge
        // shortcut actions that share the same /duplicates group.
        RouteGroupBuilder duplicatesGroup = group.MapGranitGroup("duplicates")
            .WithTags(options.DuplicatesTagName);
        duplicatesGroup.MapGranitQuery<PartyDuplicateCandidate>(configure: opts =>
        {
            opts.AuthorizationPolicy = PartiesPermissions.Parties.Read;
        });

        duplicatesGroup.MapPost("/{id:guid}/dismiss", PartyDuplicatesEndpoints.HandleDismissAsync)
            .WithName("DismissPartyDuplicateCandidate")
            .WithTags(options.DuplicatesTagName)
            .WithSummary("Marks a candidate pair as 'not a duplicate'.")
            .WithDescription("Sets DismissedAt = now on the row. Idempotent — repeated dismisses are a no-op (still 204). Dismissed pairs are durably skipped on every subsequent scan, so the admin's decision sticks. Returns 404 only when the row truly does not exist in the current tenant.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PartiesPermissions.Parties.Manage);

        duplicatesGroup.MapPost("/{id:guid}/merge", PartyDuplicatesEndpoints.HandleMergeShortcutAsync)
            .WithName("MergePartyFromDuplicateCandidate")
            .WithTags(options.DuplicatesTagName)
            .WithSummary("One-click merge from a candidate row.")
            .WithDescription("Forwards to the generic merge orchestrator. Body specifies which end of the ordered candidate pair survives (the other becomes the loser); 422 when the supplied survivorId is not part of the pair. Same Idempotency-Key + audit-write semantics as POST /parties/{survivorId}/merge.")
            .Produces<PartyMergeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem()
            .RequireAuthorization(PartiesPermissions.Parties.Merge);

        // Per-party detail listing — sits at the parent /parties/{id}/... level, not under /duplicates.
        group.MapGet("/{id:guid}/duplicate-candidates", PartyDuplicatesEndpoints.HandleListForPartyAsync)
            .WithName("ListPartyDuplicateCandidatesForParty")
            .WithTags(options.DuplicatesTagName)
            .WithSummary("Lists pending duplicate candidates that involve the given party.")
            .WithDescription("Returns every non-dismissed pair where the party is either end of the ordered (PartyId, CandidateId) tuple. Powers the per-Party detail-page warning and the create-time online detection (#1302).")
            .Produces<IReadOnlyList<PartyDuplicateCandidateResponse>>()
            .RequireAuthorization(PartiesPermissions.Parties.Read);

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
