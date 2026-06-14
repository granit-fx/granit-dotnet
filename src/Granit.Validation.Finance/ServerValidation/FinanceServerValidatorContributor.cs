using Granit.Validation.Finance.Internal;
using Granit.Validation.ServerValidation;

namespace Granit.Validation.Finance.ServerValidation;

/// <summary>
/// Registers server validators for the banking/payment identifiers owned by
/// <c>Granit.Validation.Finance</c>.
/// </summary>
internal sealed class FinanceServerValidatorContributor : IServerValidatorContributor
{
    /// <inheritdoc />
    public IEnumerable<IServerValidator> GetValidators()
    {
        // Account / creditor identifiers
        yield return new DelegatingServerValidator("Validation:Format:Iban", IbanAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:BicSwift", BicSwiftAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:SepaCreditorIdentifier", SepaCreditorIdentifierAlgorithm.IsValid);

        // Domestic routing / clearing codes
        yield return new DelegatingServerValidator("Validation:Format:AbaRouting", AbaRoutingAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:Bsb", BsbAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:CanadianRouting", CanadianRoutingAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:Ifsc", IfscAlgorithm.IsValid);
    }
}
