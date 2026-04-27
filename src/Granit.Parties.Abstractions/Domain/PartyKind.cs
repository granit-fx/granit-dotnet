namespace Granit.Parties.Domain;

/// <summary>The nature of a <see cref="Party"/> entity.</summary>
/// <remarks>
/// Mirrors Odoo's <c>res.partner</c> Person/Company distinction with a third
/// <see cref="Department"/> kind that lets a parent organisation expose internal
/// units (e.g. "Acme Corp / Accounting Department") without inflating the role flag set.
/// </remarks>
public enum PartyKind
{
    /// <summary>A natural person. Eligible for <c>UserId</c> linkage.</summary>
    Individual = 0,

    /// <summary>A legal entity (LLC, SA, SARL, …).</summary>
    Company = 1,

    /// <summary>An internal organisational unit attached to a parent <see cref="Company"/>.</summary>
    Department = 2,
}
