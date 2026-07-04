namespace Granit.Privacy.OptOut;

/// <summary>
/// State of an opt-out record (CCPA "Do Not Sell or Share").
/// </summary>
public enum OptOutState
{
    /// <summary>Opt-out is active — data must not be sold or shared.</summary>
    Active,

    /// <summary>Opt-out has been revoked by the user.</summary>
    Revoked,
}
