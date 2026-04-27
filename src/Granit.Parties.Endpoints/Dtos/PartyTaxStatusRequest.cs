namespace Granit.Parties.Endpoints.Dtos;

/// <summary>
/// Request to set a contact's customer-specific tax status — exempt, reverse-charge,
/// or standard (none of the flags set).
/// </summary>
/// <param name="IsExempt">VAT-exempt customer (NGO, public body, charity).</param>
/// <param name="ReverseCharge">B2B intra-EU reverse charge applies.</param>
/// <param name="Vatin">VAT identification number on the buyer side. Required when <paramref name="ReverseCharge"/> is <c>true</c>.</param>
/// <param name="EvidenceBlobId">Optional blob reference to supporting paperwork.</param>
public sealed record PartyTaxStatusRequest(
    bool IsExempt,
    bool ReverseCharge,
    string? Vatin,
    Guid? EvidenceBlobId);
