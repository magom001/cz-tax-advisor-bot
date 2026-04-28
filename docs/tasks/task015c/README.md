# Task 015c — PDF Tax Declaration (DPFO Form)

## Objective

Generate a PDF reproduction of the official DPFO tax declaration form, filled with data from the `TaxReturn` model.

## Work Items

1. Implement `PdfTaxDeclarationGenerator` using QuestPDF:
   - Reproduce the official DPFO form layout (2-3 pages)
   - Personal data section (name, rodné číslo, address, tax office)
   - §6 employment income
   - §7–§10 other income sections
   - §15 non-taxable deductions
   - §35ba/§35c tax credits and child benefit
   - Tax calculation (base, rate, solidarity, credits, final amount)
   - Signature and date fields
2. Czech language throughout.
3. Register in DI behind `ITaxReturnOutputService.GeneratePdfAsync`.
4. Unit test: verify PDF is non-empty and contains expected taxpayer name.

## Dependencies

- Task 015a (QuestPDF already added, patterns established)
- Understanding of the DPFO form layout (can reference the EPO PDF version)

## Expected Results

- `GeneratePdfAsync(taxReturn)` → printable PDF matching the DPFO form
- All income, deduction, and credit fields populated from TaxReturn
