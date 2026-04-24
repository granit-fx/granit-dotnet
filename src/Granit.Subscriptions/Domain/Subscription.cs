using Granit.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Events;
using Granit.Workflow.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A tenant's subscription to a plan, managing lifecycle, seats, and billing periods.
/// </summary>
/// <remarks>
/// <para>
/// The subscription lifecycle is managed by <c>Granit.Workflow</c> via the
/// <see cref="SubscriptionStatus"/> FSM. All state transitions are recorded as
/// immutable <see cref="WorkflowTransitionRecord"/> entries (ISO 27001).
/// </para>
/// <para>
/// Behavior methods are <b>idempotent</b>: if the subscription is already in the
/// target state, the method returns <c>false</c> without raising events. This
/// prevents webhook ping-pong loops with external providers.
/// </para>
/// </remarks>
public sealed class Subscription : AuditedAggregateRoot, IWorkflowStateful, IMultiTenant
{
    private readonly List<SubscriptionSeat> _seats = [];
    private readonly List<SubscriptionExternalMapping> _externalMappings = [];
    private readonly List<SubscriptionPhase> _phases = [];

    private Subscription() { }

    /// <summary>Creates a new subscription in Trial or Active status.</summary>
    /// <param name="id">Unique subscription identifier.</param>
    /// <param name="tenantId">Owning tenant identifier.</param>
    /// <param name="planId">Plan the subscription is for.</param>
    /// <param name="currency">ISO 4217 currency code (e.g., "EUR").</param>
    /// <param name="period">Initial billing period boundaries.</param>
    /// <param name="trialEndsAt">Trial expiry. When set, the subscription starts in Trial status.</param>
    /// <param name="planPriceId">Pinned price version for grandfathering. Null for dynamic pricing.</param>
    public static Subscription Create(
        SubscriptionId id,
        Guid tenantId,
        PlanId planId,
        string currency,
        SubscriptionPeriod period,
        DateTimeOffset? trialEndsAt = null,
        Guid? planPriceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(period);

        SubscriptionStatus initialStatus = trialEndsAt.HasValue
            ? SubscriptionStatus.Trial
            : SubscriptionStatus.Active;

        var subscription = new Subscription
        {
            Id = id,
            TenantId = tenantId,
            PlanId = planId,
            Currency = currency.ToUpperInvariant(),
            Status = initialStatus,
            CurrentPeriodStart = period.Start,
            CurrentPeriodEnd = period.End,
            BillingCycleAnchor = period.BillingCycleAnchor,
            TrialEndsAt = trialEndsAt,
            CancelAtPeriodEnd = false,
            PlanPriceId = planPriceId,
        };

        subscription.AddDomainEvent(new SubscriptionCreatedEvent(id, planId, tenantId));
        subscription.AddDistributedEvent(new SubscriptionCreatedEto(
            id, planId, tenantId, trialEndsAt.HasValue));

        return subscription;
    }

    /// <summary>The plan this subscription is for (FK, including Archived plans).</summary>
    public PlanId PlanId { get; private set; } = null!;

    /// <summary>Current lifecycle status (FSM via Granit.Workflow).</summary>
    public SubscriptionStatus Status { get; private set; }

    /// <summary>Start of the current billing period.</summary>
    public DateTimeOffset CurrentPeriodStart { get; private set; }

    /// <summary>End of the current billing period.</summary>
    public DateTimeOffset CurrentPeriodEnd { get; private set; }

    /// <summary>Anchor date for billing cycle calculations (Stripe pattern).</summary>
    public DateTimeOffset BillingCycleAnchor { get; private set; }

    /// <summary>When the trial ends. Null if no trial.</summary>
    public DateTimeOffset? TrialEndsAt { get; private set; }

    /// <summary>Whether to cancel at the end of the current billing period.</summary>
    public bool CancelAtPeriodEnd { get; private set; }

    /// <summary>When the subscription was cancelled. Null if not cancelled.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Reason for cancellation.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>ISO 4217 currency code selected at subscription creation (e.g., "EUR").</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// Pinned price version. When set, billing uses this specific <see cref="PlanPrice"/>
    /// instead of resolving dynamically from the plan's current prices (grandfathering).
    /// Null for subscriptions created before price versioning was enabled.
    /// </summary>
    public Guid? PlanPriceId { get; private set; }

    /// <summary>Number of failed payment retry attempts (dunning). Reset on successful payment.</summary>
    public int DunningAttempt { get; private set; }

    /// <summary>Assigned seats (user assignments).</summary>
    public IReadOnlyList<SubscriptionSeat> Seats => _seats.AsReadOnly();

    /// <summary>External provider mappings (Stripe, Mollie, etc.).</summary>
    public IReadOnlyList<SubscriptionExternalMapping> ExternalMappings => _externalMappings.AsReadOnly();

    /// <summary>
    /// Scheduled timeline segments. Each <see cref="SubscriptionPhase"/> covers
    /// <c>[StartDate, EndDate)</c> and pins the active plan for that interval —
    /// enabling ramp deals (<c>trial → standard → enterprise</c>). Empty by
    /// default; the orchestrator falls back to <see cref="PlanId"/> when no
    /// phase covers the billing instant (single-plan backward compat).
    /// </summary>
    public IReadOnlyList<SubscriptionPhase> Phases => _phases;

    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get; set; }

    // ── IWorkflowStateful ──────────────────────────────────────────────

    static string IWorkflowStateful.StatusPropertyName => nameof(Status);

    static string IWorkflowStateful.WorkflowEntityType => "Subscription";

    /// <inheritdoc />
    public string GetWorkflowEntityId() => Id.ToString();

    // ── Behavior methods (idempotent — return false if already in target state) ──

    /// <summary>Activates the subscription (trial conversion or payment confirmed).</summary>
    public bool Activate()
    {
        if (Status == SubscriptionStatus.Active)
        {
            return false;
        }

        EnsureTransitionAllowed(SubscriptionStatus.Active);
        Status = SubscriptionStatus.Active;
        AddDistributedEvent(new SubscriptionActivatedEto(Id, PlanId, TenantId!.Value));
        return true;
    }

    /// <summary>Marks the subscription as past due (payment failed).</summary>
    public bool MarkPastDue()
    {
        if (Status == SubscriptionStatus.PastDue)
        {
            return false;
        }

        EnsureTransitionAllowed(SubscriptionStatus.PastDue);
        Status = SubscriptionStatus.PastDue;
        return true;
    }

    /// <summary>Suspends the subscription (all payment retries exhausted).</summary>
    public bool Suspend()
    {
        if (Status == SubscriptionStatus.Suspended)
        {
            return false;
        }

        EnsureTransitionAllowed(SubscriptionStatus.Suspended);
        Status = SubscriptionStatus.Suspended;
        AddDistributedEvent(new SubscriptionSuspendedEto(Id, PlanId, TenantId!.Value));
        return true;
    }

    /// <summary>Cancels the subscription.</summary>
    public bool Cancel(string? reason, DateTimeOffset cancelledAt)
    {
        if (reason is { Length: > 500 })
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "Cancellation reason must not exceed 500 characters.");
        }

        if (Status == SubscriptionStatus.Cancelled)
        {
            return false;
        }

        EnsureTransitionAllowed(SubscriptionStatus.Cancelled);
        Status = SubscriptionStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancellationReason = reason;
        CancelAtPeriodEnd = false;
        AddDistributedEvent(new SubscriptionCancelledEto(Id, PlanId, TenantId!.Value, reason));
        return true;
    }

    /// <summary>Expires the subscription (trial ended without conversion).</summary>
    public bool Expire()
    {
        if (Status == SubscriptionStatus.Expired)
        {
            return false;
        }

        EnsureTransitionAllowed(SubscriptionStatus.Expired);
        Status = SubscriptionStatus.Expired;
        AddDistributedEvent(new SubscriptionExpiredEto(Id, PlanId, TenantId!.Value));
        return true;
    }

    /// <summary>Schedules cancellation at the end of the current billing period.</summary>
    public void ScheduleCancelAtPeriodEnd()
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trial))
        {
            throw new InvalidOperationException(
                $"Cannot schedule cancel at period end for subscription '{Id}' in '{Status}' status.");
        }

        CancelAtPeriodEnd = true;
    }

    /// <summary>Removes the scheduled cancellation at period end.</summary>
    public void UnscheduleCancelAtPeriodEnd() =>
        CancelAtPeriodEnd = false;

    /// <summary>Increments the dunning attempt counter after a payment failure.</summary>
    public void IncrementDunningAttempt() => DunningAttempt++;

    /// <summary>Resets the dunning counter after a successful payment.</summary>
    public void ResetDunning() => DunningAttempt = 0;

    /// <summary>Changes the subscription plan.</summary>
    public void ChangePlan(PlanId newPlanId)
    {
        ArgumentNullException.ThrowIfNull(newPlanId);

        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trial))
        {
            throw new InvalidOperationException(
                $"Cannot change plan for subscription '{Id}' in '{Status}' status.");
        }

        PlanId oldPlanId = PlanId;
        PlanId = newPlanId;
        AddDistributedEvent(new SubscriptionPlanChangedEto(Id, oldPlanId, newPlanId, TenantId!.Value));
    }

    /// <summary>Advances to the next billing period.</summary>
    public void AdvancePeriod(DateTimeOffset newPeriodStart, DateTimeOffset newPeriodEnd)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trial or SubscriptionStatus.PastDue))
        {
            throw new InvalidOperationException(
                $"Cannot advance billing period for subscription '{Id}' in '{Status}' status.");
        }

        if (newPeriodStart < CurrentPeriodEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(newPeriodStart), "New period must not start before the current period ends.");
        }

        if (newPeriodEnd <= newPeriodStart)
        {
            throw new ArgumentOutOfRangeException(nameof(newPeriodEnd), "Period end must be after period start.");
        }

        CurrentPeriodStart = newPeriodStart;
        CurrentPeriodEnd = newPeriodEnd;
        AddDistributedEvent(new BillingCycleCompletedEto(
            Id, PlanId, TenantId!.Value, newPeriodStart, newPeriodEnd));
    }

    /// <summary>
    /// Migrates this subscription to a new price version. Only allowed for Active or Trial subscriptions.
    /// Returns <c>false</c> if already pinned to the same price.
    /// </summary>
    public bool MigratePrice(Guid newPlanPriceId)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trial))
        {
            throw new InvalidOperationException(
                $"Cannot migrate price for subscription '{Id}' in '{Status}' status.");
        }

        if (PlanPriceId == newPlanPriceId)
        {
            return false;
        }

        Guid? oldPlanPriceId = PlanPriceId;
        PlanPriceId = newPlanPriceId;
        AddDistributedEvent(new SubscriptionPriceMigratedEto(
            Id, TenantId!.Value, oldPlanPriceId, newPlanPriceId));
        return true;
    }

    // ── Seat management ────────────────────────────────────────────────

    /// <summary>Assigns a seat to a user. Enforces duplicate-user and seat-limit guards.</summary>
    /// <param name="seat">The seat to assign.</param>
    /// <param name="seatLimit">Maximum allowed seats for the plan. Null means unlimited.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the user already holds a seat or when the plan seat limit has been reached.
    /// </exception>
    public void AssignSeat(SubscriptionSeat seat, int? seatLimit = null)
    {
        ArgumentNullException.ThrowIfNull(seat);

        if (_seats.Any(s => s.UserId == seat.UserId))
        {
            throw new InvalidOperationException(
                $"User '{seat.UserId}' already has a seat on subscription '{Id}'.");
        }

        if (seatLimit.HasValue && _seats.Count >= seatLimit.Value)
        {
            throw new InvalidOperationException(
                $"Subscription '{Id}' has reached the seat limit of {seatLimit.Value}.");
        }

        _seats.Add(seat);
    }

    /// <summary>Revokes a seat from a user.</summary>
    public bool RevokeSeat(Guid userId)
    {
        SubscriptionSeat? seat = _seats.FirstOrDefault(s => s.UserId == userId);
        if (seat is null)
        {
            return false;
        }

        _seats.Remove(seat);
        return true;
    }

    // ── External mappings ──────────────────────────────────────────────

    /// <summary>Adds an external provider mapping.</summary>
    public void AddExternalMapping(SubscriptionExternalMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _externalMappings.Add(mapping);
    }

    // ── Phase scheduling ───────────────────────────────────────────────

    /// <summary>
    /// Adds a scheduled phase to the subscription's timeline. Throws if the new
    /// phase overlaps any existing phase — overlap is defined on the half-open
    /// interval <c>[StartDate, EndDate)</c>; touching endpoints
    /// (<c>existing.EndDate == new.StartDate</c>) are allowed.
    /// </summary>
    public void AddPhase(SubscriptionPhase phase)
    {
        ArgumentNullException.ThrowIfNull(phase);

        if (phase.SubscriptionId != Id)
        {
            throw new InvalidOperationException(
                $"Phase '{phase.Id}' belongs to subscription '{phase.SubscriptionId}', not '{Id}'.");
        }

        foreach (SubscriptionPhase existing in _phases)
        {
            if (Overlaps(existing, phase))
            {
                throw new InvalidOperationException(
                    $"Phase [{phase.StartDate:O}, {phase.EndDate?.ToString("O") ?? "∞"}) overlaps existing phase '{existing.Id}'.");
            }
        }

        _phases.Add(phase);
    }

    /// <summary>Removes a previously-added phase. Returns <c>true</c> when a phase was actually removed.</summary>
    public bool RemovePhase(Guid phaseId)
    {
        int removed = _phases.RemoveAll(p => p.Id == phaseId);
        return removed > 0;
    }

    /// <summary>
    /// Returns the phase that covers <paramref name="instant"/>, or <c>null</c>
    /// when no phase covers it (caller falls back to <see cref="PlanId"/>).
    /// At most one phase covers any given instant — guaranteed by
    /// <see cref="AddPhase"/>'s overlap check.
    /// </summary>
    public SubscriptionPhase? GetActivePhase(DateTimeOffset instant) =>
        _phases.FirstOrDefault(p => p.Covers(instant));

    /// <summary>
    /// True when two phases share at least one instant under the half-open
    /// interval semantics. Touching endpoints (<c>a.EndDate == b.StartDate</c>)
    /// do not overlap.
    /// </summary>
    private static bool Overlaps(SubscriptionPhase a, SubscriptionPhase b)
    {
        DateTimeOffset aEnd = a.EndDate ?? DateTimeOffset.MaxValue;
        DateTimeOffset bEnd = b.EndDate ?? DateTimeOffset.MaxValue;
        return a.StartDate < bEnd && b.StartDate < aEnd;
    }

    // ── Private helpers ────────────────────────────────────────────────

    private void EnsureTransitionAllowed(SubscriptionStatus target)
    {
        bool allowed = (Status, target) switch
        {
            (SubscriptionStatus.Trial, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.Trial, SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.PastDue) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.PastDue, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.PastDue, SubscriptionStatus.Suspended) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled) => true,
            _ => false,
        };

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Transition from '{Status}' to '{target}' is not allowed for subscription '{Id}'.");
        }
    }
}
