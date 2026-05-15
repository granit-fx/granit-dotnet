namespace Granit.DataExchange.Endpoints.Workspaces;

/// <summary>Feature name constants for the data-exchange module (per ADR-057).</summary>
public static class DataExchangeFeatures
{
    /// <summary>Imports dashboard — paired with <c>/data-exchange/imports</c>.</summary>
    public const string Imports = "data-exchange.imports";

    /// <summary>Exports dashboard — paired with <c>/data-exchange/exports</c>.</summary>
    public const string Exports = "data-exchange.exports";
}
