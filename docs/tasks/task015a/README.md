# Task 015a — Stock Calculation Table (PDF)

## Objective

Generate a Czech-language PDF table showing all stock compensation transactions (RSU vesting, ESPP purchases, dividends, tax withheld) with both daily and uniform ČNB exchange rates. This is the supporting document submitted alongside the tax return.

## Work Items

1. Add NuGet package: `QuestPDF`.
2. Create `ITaxReturnOutputService` interface in Application layer.
3. Implement `StockCalculationTableGenerator`:
   - Table columns: Date | Type | Security | Qty | Price (USD) | Amount (USD) | ČNB Daily Rate | Amount (CZK daily) | §38 Uniform Rate | Amount (CZK uniform)
   - Group by transaction type (Vesting, ESPP, Dividends, Tax Withheld)
   - Subtotals per group
   - Summary section: total §6 income, total §10 income, total foreign tax paid
   - Header with taxpayer info (name, rodné číslo, tax year)
   - Czech language labels
   - Port layout from existing TaxAdvisor tool (`Q:\projects\TaxAdvisor\src\TaxAdvisor.Lib\Printing\PdfPrinter.cs`)
4. Register in DI.
5. Write unit test: generate table from known `TaxReturn` → verify PDF is non-empty.

## Expected Results

- `StockCalculationTableGenerator.GenerateAsync(taxReturn)` → `byte[]` PDF
- Table contains all stock transactions with both exchange rate columns
- Subtotals are correct
- Czech headings and formatting
