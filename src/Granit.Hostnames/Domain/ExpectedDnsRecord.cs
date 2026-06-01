namespace Granit.Hostnames.Domain;

/// <summary>
/// A DNS record that must be present for verification to succeed.
/// Stored as JSON on <see cref="ManagedHostname.ExpectedDnsRecords"/>.
/// </summary>
/// <param name="RecordType">Type of DNS record (A, AAAA, CNAME, TXT).</param>
/// <param name="Name">Fully-qualified DNS name to query (e.g. <c>"_granit-challenge.acme.com"</c>).</param>
/// <param name="Value">Expected record value to match.</param>
public sealed record ExpectedDnsRecord(DnsRecordType RecordType, string Name, string Value);
