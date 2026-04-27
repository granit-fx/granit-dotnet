namespace Granit.Parties.Domain;

/// <summary>The functional type of a <see cref="PartyPhone"/> attached to a <see cref="Party"/>.</summary>
public enum PhoneKind
{
    /// <summary>Mobile / cell phone.</summary>
    Mobile = 0,

    /// <summary>Work / business phone (office landline, freelancer mobile, …). Matches vCard <c>TYPE=work</c>.</summary>
    Work = 1,

    /// <summary>Home landline.</summary>
    Home = 2,

    /// <summary>Anything else (fax, secondary line, …).</summary>
    Other = 3,
}
