# Granit.Invoicing.Odoo

Odoo external accounting sync for Granit.Invoicing.

## Features

- **Push**: invoice → Odoo `account.move` on finalization
- **Pull**: poll Odoo for payment status updates
- **Model**: maps to Odoo's `account.move` (unified invoice/credit note)

## Configuration

```json
{
  "Invoicing": {
    "Odoo": {
      "BaseUrl": "https://mycompany.odoo.com",
      "Database": "mycompany",
      "Login": "admin@mycompany.com",
      "ApiKey": "odoo-api-key-xxx",
      "DefaultJournalId": 1
    }
  }
}
```

## How it works

1. Invoice finalized in Granit → `IInvoiceSyncProvider.SyncAsync()` called
2. Odoo JSON-RPC: `account.move.create()` with line items
3. ExternalReference stored (provider: "odoo", externalId: Odoo record ID)
4. Background job polls `account.move.read()` for payment_state changes
