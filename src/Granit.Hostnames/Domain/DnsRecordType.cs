namespace Granit.Hostnames.Domain;

/// <summary>DNS record types relevant to hostname verification.</summary>
public enum DnsRecordType
{
    /// <summary>IPv4 address record.</summary>
    A,

    /// <summary>IPv6 address record.</summary>
    Aaaa,

    /// <summary>Canonical name record (alias).</summary>
    Cname,

    /// <summary>Text record, used for verification challenges.</summary>
    Txt,
}
