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

    private Subscription() { }

    /// <summary>Creates a new subscription in Trial or Active status.</summary>
    public static Subscription Create(
        Guid id,
        Guid tenantId,
        PlanId planId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset billingCycleAnchor,
        DateTimeOffset? trialEndsAt = null)
    {
        SubscriptionStatus initialStatus = trialEndsAt.HasValue
            ? SubscriptionStatus.Trial
            : SubscriptionStatus.Active;

        var subscription = new Subscription
        {
            Id = id,
            TenantId = tenantId,
            PlanId = planId,
            Status = initialStatus,
            CurrentPeriodStart = periodStart,
            CurrentPeriodEnd = periodEnd,
            BillingCycleAnchor = billingCycleAnchor,
            TrialEndsAt = trialEndsAt,
            CancelAtPeriodEnd = false,
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

    /// <summary>Assigned seats (user assignments).</summary>
    public IReadOnlyList<SubscriptionSeat> Seats => _seats.AsReadOnly();

    /// <summary>External provider mappings (Stripe, Mollie, etc.).</summary>
    public IReadOnlyList<SubscriptionExternalMapping> ExternalMappings => _externalMappings.AsReadOnly();

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
        AddDistributedEvent(new SubscriptionSuspendedEto(Id, TenantId!.Value));
        return true;
    }

    /// <summary>Cancels the subscription.</summary>
    public bool Cancel(string? reason, DateTimeOffset cancelledAt)
    {
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
        AddDistributedEvent(new SubscriptionExpiredEto(Id, TenantId!.Value));
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
    public void UnscheduleCancelAtPeriodEnd()
    {
        CancelAtPeriodEnd = false;
    }

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

        CurrentPeriodStart = newPeriodStart;
        CurrentPeriodEnd = newPeriodEnd;
        AddDistributedEvent(new BillingCycleCompletedEto(
            Id, PlanId, TenantId!.Value, newPeriodStart, newPeriodEnd));
    }

    // ── Seat management ────────────────────────────────────────────────

    /// <summary>Assigns a seat to a user.</summary>
    public void AssignSeat(SubscriptionSeat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
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
