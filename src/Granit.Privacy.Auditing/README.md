# Granit.Privacy.Auditing

Glue package wiring `Granit.Privacy`'s `IPrivacyExportAuditWriter` to `Granit.Auditing`'s `IAuditingWriter`. Registering this module makes every personal-data-export lifecycle event (requested / completed / shard-downloaded / failed) land in the audit trail as an `AuditEntry`, satisfying GDPR Art. 30 (ROPA) and ISO 27001 A.5.34 evidence requirements.

Without this module, `Granit.Privacy` uses a no-op writer — the export flow still works, but no audit row is recorded.
