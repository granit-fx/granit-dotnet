namespace Granit.Hostnames.Domain;

/// <summary>DNS record types relevant to hostname verification.</summary>
public enum DnsRecordType
{
    /// <summary>IPv4 address record.</summary>
    A = 0,

    /// <summary>IPv6 address record.</summary>
    Aaaa = 1,

    /// <summary>Canonical name record (alias).</summary>
    Cname = 2,

    /// <summary>Text record, used for verification challenges.</summary>
    Txt = 3,
}
