# Security Policy

## Supported Versions

| Version | Supported |
| ------- | --------- |
| latest  | Yes       |

Only the latest released version receives security updates.
We recommend staying up to date.

## Reporting a Vulnerability

**DO NOT open a public GitHub issue for security vulnerabilities.**

### Preferred: GitHub Private Security Advisory

Use [GitHub Private Security Advisories](https://github.com/granit-fx/granit-dotnet/security/advisories/new)
to report vulnerabilities confidentially. This enables coordinated disclosure
and automatic CVE assignment through MITRE.

### Alternative: Email

Send a detailed report to **<security@granit-fx.dev>**.

### What to include

- Affected package(s) and version(s)
- Steps to reproduce (proof of concept if possible)
- Potential impact and attack scenario
- Any suggested mitigations

## Response SLA

| Severity     | Acknowledgment | Assessment | Patch target | Public disclosure    |
| ------------ | -------------- | ---------- | ------------ | -------------------- |
| Critical     | 24 hours       | 48 hours   | 7 days       | 14 days after patch  |
| High         | 48 hours       | 7 days     | 30 days      | 30 days after patch  |
| Medium / Low | 48 hours       | 14 days    | 90 days      | 90 days after patch  |

We follow a **coordinated disclosure** model: vulnerabilities are kept private
until a fix is available and deployed by affected users.

## What qualifies as a security vulnerability?

- Authentication or authorization bypass
- Injection vulnerabilities (SQL, command, LDAP, etc.)
- Sensitive data exposure (secrets, PII leakage, insecure defaults)
- Cryptographic weaknesses (weak algorithms, broken key management)
- Dependency vulnerabilities (HIGH or CRITICAL severity per CVSS)
- Privilege escalation
- Multi-tenancy isolation bypass (GDPR risk)

## Out of scope

- Vulnerabilities in applications *built with* Granit (report to the application owner)
- Issues requiring physical access to infrastructure
- Social engineering attacks
- Denial-of-service through resource exhaustion
- Security bugs in already-known-vulnerable dependency versions (use Dependabot)

## Security Design

Granit is built with security by design:

- **No plaintext secrets** — all secrets managed via Vault or environment variables
- **Encryption** — data encrypted at rest (AES-256) and in transit (TLS)
- **Audit trail** — all sensitive operations are logged with full traceability
- **GDPR compliance** — data minimization, right to erasure, pseudonymization
- **RBAC** — fine-grained three-segment permission model (`[Group].[Resource].[Action]`)
- **Multi-tenancy isolation** — strict tenant data separation enforced at the ORM level
- **Supply chain** — SBOM generated per release, SLSA provenance attestation on every release

## Recognition

We appreciate responsible disclosure. Reporters of valid vulnerabilities will be
credited in the release notes (unless they prefer to remain anonymous).
