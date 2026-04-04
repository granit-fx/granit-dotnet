using System.ComponentModel.DataAnnotations;

namespace Granit.Invoicing.Odoo.Options;

/// <summary>Configuration for the Odoo accounting sync provider.</summary>
public sealed class OdooOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Invoicing:Odoo";

    /// <summary>Odoo instance URL (e.g., "https://mycompany.odoo.com").</summary>
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Odoo database name.</summary>
    [Required]
    public string Database { get; set; } = string.Empty;

    /// <summary>Odoo API key (Settings → API Keys) or user password.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Odoo user login (email).</summary>
    [Required]
    public string Login { get; set; } = string.Empty;

    /// <summary>Default Odoo journal ID for synced invoices (e.g., 1 for "Customer Invoices").</summary>
    public int DefaultJournalId { get; set; } = 1;
}
