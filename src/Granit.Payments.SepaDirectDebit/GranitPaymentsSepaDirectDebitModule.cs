using Granit.Modularity;
using Granit.Timing;

namespace Granit.Payments.SepaDirectDebit;

/// <summary>
/// SEPA Direct Debit abstractions: mandate lifecycle, collection tracking,
/// and provider interfaces. Add .Internal, .GoCardless, or .Twikey for a concrete provider.
/// </summary>
[DependsOn(
    typeof(GranitPaymentsModule),
    typeof(GranitTimingModule))]
public sealed class GranitPaymentsSepaDirectDebitModule : GranitModule;
