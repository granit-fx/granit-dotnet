namespace Granit.ArchitectureTests;

/// <summary>
/// Canonical list of <c>*Request</c> DTO parameters that are deliberately required despite
/// being nullable, exempted from <see cref="DtoConventionTests.Nullable_request_parameters_must_have_a_default"/>.
/// </summary>
/// <remarks>
/// Keyed <c>{RequestTypeName}.{ParameterName}</c>. Each entry needs a one-line justification
/// (inline comment) explaining why the value must be present in the payload yet may be null.
/// The default action for a nullable optional input is to add <c>= null</c>, not an exemption.
/// </remarks>
internal static class RequestDtoNullableDefaultExemptions
{
    public static readonly HashSet<string> RequiredButNullable = new(StringComparer.Ordinal)
    {
        // Explicit tenancy choice: the caller must consciously send a value (a tenant id, or `null`
        // for a global/host-level role) — omitting the key is not a valid create request. (#2546)
        "RoleCreateRequest.TenantId",
    };
}
