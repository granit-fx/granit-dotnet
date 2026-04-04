using System.ComponentModel.DataAnnotations;

namespace Granit.Payments.SepaDirectDebit.Twikey.Options;

/// <summary>Configuration for the Twikey SEPA DD provider.</summary>
public sealed class TwikeyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:SepaDirectDebit:Twikey";

    /// <summary>Twikey API key.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Twikey contract template ID.</summary>
    [Required]
    public string ContractTemplateId { get; set; } = string.Empty;

    /// <summary>Use Twikey test environment.</summary>
    public bool UseSandbox { get; set; }
}
