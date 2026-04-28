# Task 015 — Multi-Artifact Output: XML, PDF & Calculation Tables

## Objective

Generate the final tax filing artifacts from a completed `TaxReturn`:
1. **EPO XML** — uploadable to the Czech Financial Administration portal
2. **PDF Tax Declaration** — the official DPFO form filled in
3. **RSU/ESPP/Dividends Calculation Table** — supporting document for the tax office showing stock compensation details with ČNB exchange rates

## XSD Schema Management

The Czech Financial Administration publishes XSD schemas on the EPO portal (`https://adisspr.mfcr.cz/pmd/epo/formulare`) annually. These change every tax year.

### Process
1. Download the current year XSD for "Přiznání k dani z příjmů fyzických osob" (DPFO).
2. Store in `src/core/TaxAdvisorBot.Infrastructure/Output/Schemas/dpfo-{year}.xsd`.
3. Generate C# classes: `dotnet tool install --global dotnet-xscgen` then `xscgen dpfo-2025.xsd -n TaxAdvisorBot.Infrastructure.Output.Xml`.
4. Wrap generated classes behind an `IXmlTaxReturnGenerator` interface to insulate from annual changes.
5. When the schema changes next year, regenerate classes and update the mapping — the interface stays the same.

## Work Items

### Interface (Application layer)
1. `ITaxReturnOutputService`:
   - `GenerateXmlAsync(TaxReturn) → byte[]` — EPO XML
   - `GeneratePdfAsync(TaxReturn) → byte[]` — DPFO PDF form
   - `GenerateCalculationTableAsync(TaxReturn) → byte[]` — RSU/ESPP/dividends PDF table
   - `GenerateAllAsync(TaxReturn) → TaxReturnOutputBundle` — all artifacts as a zip or bundle

### XML Generator
2. Download and store DPFO XSD schema.
3. Generate C# classes from XSD.
4. Implement `EpoXmlGenerator`:
   - Maps `TaxReturn` fields to EPO XML schema elements.
   - Handles §6 income, §10 share sales, §15 deductions, §35ba/§35c credits.
   - Validates output against XSD before returning.
5. Handle the foreign income attachment (Příloha č. 3) for §38f tax credit method.

### PDF Tax Declaration
6. Implement `PdfTaxDeclarationGenerator` using QuestPDF:
   - Reproduces the official DPFO form layout.
   - All sections filled from `TaxReturn` data.
   - Czech language, proper formatting (rodné číslo, DIČ, tax office code).

### Calculation Table (supporting document)
7. Implement `StockCalculationTableGenerator` using QuestPDF:
   - Table columns: Date | Type (RSU/ESPP/Dividend/Tax) | Security | Qty | Price (USD) | Amount (USD) | ČNB Rate | Amount (CZK) | §38 Uniform Rate | Amount (CZK uniform)
   - Subtotals per type (vesting total, ESPP discount total, dividend total, tax withheld total).
   - Summary section: total §6 income from stock comp, total §10 income from sales, total foreign tax paid.
   - Port table layout from existing TaxAdvisor tool (`Q:\projects\TaxAdvisor`).

### API & UI
8. Add endpoints:
   - `POST /api/output/xml` — generate and download XML
   - `POST /api/output/pdf` — generate and download PDF
   - `POST /api/output/table` — generate and download calculation table
   - `POST /api/output/all` — generate all as zip
9. Add "Generate Output" section to web UI with download buttons.

### Tests
10. Unit test XML generation: known `TaxReturn` → validate output against XSD.
11. Unit test PDF generation: verify non-empty, contains expected text.
12. Unit test calculation table: verify totals match `TaxReturn` computed fields.

## Expected Results

- User completes tax return via agent conversation.
- Clicks "Generate" → downloads:
  - `dpfo-2025.xml` — ready to upload at EPO portal
  - `dpfo-2025.pdf` — printable tax declaration
  - `stock-calculation-2025.pdf` — supporting document for the tax office
- XML validates against the official XSD schema.
- Calculation table shows both daily and uniform ČNB rates.
