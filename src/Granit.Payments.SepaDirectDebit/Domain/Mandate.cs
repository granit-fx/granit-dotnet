using Granit.DataProtection;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Payments.SepaDirectDebit.Domain;

/// <summary>
/// SEPA Direct Debit mandate — customer consent to pull funds from their bank account.
/// </summary>
public sealed class Mandate : AuditedAggregateRoot, IMultiTenant
{
    private readonly List<DirectDebitPayment> _collections = [];

    private Mandate() { }

    /// <summary>Creates a new mandate in Pending status.</summary>
    public static Mandate Create(
        Guid id,
        Guid tenantId,
        string mandateReference,
        SddScheme scheme,
        string debtorName,
        string debtorIban,
        string creditorId,
        string? debtorBic = null,
        string? providerName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mandateReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(debtorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(debtorIban);
        ArgumentException.ThrowIfNullOrWhiteSpace(creditorId);

        return new Mandate
        {
            Id = id,
            TenantId = tenantId,
            MandateReference = mandateReference,
            Scheme = scheme,
            DebtorName = debtorName,
            DebtorIban = debtorIban,
            DebtorBic = debtorBic,
            CreditorId = creditorId,
            Status = MandateStatus.Pending,
            ProviderName = providerName,
        };
    }

    /// <summary>Unique mandate reference (e.g., "SDD-A1B2C3D4").</summary>
    public string MandateReference { get; private set; } = string.Empty;

    /// <summary>Current mandate status.</summary>
    public MandateStatus Status { get; private set; }

    /// <summary>SDD scheme (Core or B2B).</summary>
    public SddScheme Scheme { get; private set; }

    /// <summary>Debtor (customer) name.</summary>
    [SensitiveData]
    public string DebtorName { get; private set; } = string.Empty;

    /// <summary>Debtor IBAN.</summary>
    [SensitiveData]
    public string DebtorIban { get; private set; } = string.Empty;

    /// <summary>Debtor BIC/SWIFT (optional).</summary>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string? DebtorBic { get; private set; }

    /// <summary>SEPA Creditor Identifier.</summary>
    public string CreditorId { get; private set; } = string.Empty;

    /// <summary>When the mandate was signed by the customer.</summary>
    public DateTimeOffset? SignedAt { get; private set; }

    /// <summary>When the mandate became active.</summary>
    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>When the mandate was cancelled.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Last successful collection date.</summary>
    public DateTimeOffset? LastCollectionAt { get; private set; }

    /// <summary>Provider name (null = self-hosted).</summary>
    public string? ProviderName { get; private set; }

    /// <summary>External provider mandate ID (e.g., GoCardless MD_xxx).</summary>
    public string? ProviderMandateId { get; private set; }

    /// <summary>Collections against this mandate.</summary>
    public IReadOnlyList<DirectDebitPayment> Collections => _collections.AsReadOnly();

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>Activates the mandate after customer signature.</summary>
    public bool Activate(DateTimeOffset signedAt)
    {
        if (Status != MandateStatus.Pending && Status != MandateStatus.Suspended)
        {
            return false;
        }

        Status = MandateStatus.Active;
        SignedAt ??= signedAt;
        ActivatedAt = signedAt;
        return true;
    }

    /// <summary>Suspends the mandate (e.g., after failed collection).</summary>
    public bool Suspend()
    {
        if (Status != MandateStatus.Active)
        {
            return false;
        }

        Status = MandateStatus.Suspended;
        return true;
    }

    /// <summary>Cancels the mandate.</summary>
    public bool Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is MandateStatus.Cancelled or MandateStatus.Expired)
        {
            return false;
        }

        Status = MandateStatus.Cancelled;
        CancelledAt = cancelledAt;
        return true;
    }

    /// <summary>Marks the mandate as failed (bank rejection).</summary>
    public bool MarkFailed()
    {
        if (Status != MandateStatus.Pending)
        {
            return false;
        }

        Status = MandateStatus.Failed;
        return true;
    }

    /// <summary>Expires the mandate (36 months without collection).</summary>
    public bool Expire()
    {
        if (Status != MandateStatus.Active)
        {
            return false;
        }

        Status = MandateStatus.Expired;
        return true;
    }

    /// <summary>Sets the external provider mandate ID.</summary>
    public void SetProviderMandateId(string providerMandateId) =>
        ProviderMandateId = providerMandateId;

    /// <summary>Adds a collection attempt.</summary>
    public void AddCollection(DirectDebitPayment collection)
    {
        if (Status != MandateStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot add collection to mandate '{Id}' in '{Status}' status.");
        }

        _collections.Add(collection);
    }

    /// <summary>Records a successful collection.</summary>
    public void RecordSuccessfulCollection(DateTimeOffset settledAt) =>
        LastCollectionAt = settledAt;
}
