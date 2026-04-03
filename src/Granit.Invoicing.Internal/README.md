# Granit.Invoicing.Internal

Self-hosted invoice PDF generation for Granit.Invoicing.

## How it works

1. Invoice data is mapped to `InvoiceTemplateData`
2. Scriban HTML template renders the data (`Templates/Invoicing.Invoice/body.html`)
3. `Granit.DocumentGeneration.Pdf` converts HTML → PDF
4. PDF returned as `InvoiceDocumentResult`

## Customization

Override the default template by registering a custom `Invoicing.Invoice`
template in your host application via `AddEmbeddedTemplates()` or
`ITemplateResolver` (database-stored templates take precedence).
