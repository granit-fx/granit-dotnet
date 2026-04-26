namespace Granit.Contacts.Domain;

/// <summary>The functional type of a <see cref="ContactPhone"/> attached to a <see cref="Contact"/>.</summary>
public enum PhoneKind
{
    /// <summary>Mobile / cell phone.</summary>
    Mobile = 0,

    /// <summary>Office / work landline.</summary>
    Office = 1,

    /// <summary>Home landline.</summary>
    Home = 2,

    /// <summary>Anything else (fax, secondary line, …).</summary>
    Other = 3,
}
