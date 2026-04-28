# Task 015b — EPO XML Generation

## Objective

Generate a valid XML file in the Czech Financial Administration's EPO schema for electronic submission of the DPFO tax return.

## Work Items

1. Download the DPFO XSD schema from EPO portal (`https://adisspr.mfcr.cz/pmd/epo/formulare`).
2. Store schema in `src/core/TaxAdvisorBot.Infrastructure/Output/Schemas/`.
3. Install `dotnet-xscgen` tool and generate C# classes from XSD.
4. Implement `EpoXmlGenerator`:
   - Maps `TaxReturn` → EPO XML schema classes
   - §6 income section
   - §10 other income (share sales)
   - §15 deductions
   - §35ba credits, §35c child benefit
   - Příloha č. 3 (foreign income, §38f tax credit)
   - Personal data (name, rodné číslo, tax office code)
5. Validate generated XML against XSD before returning.
6. Wrap behind `ITaxReturnOutputService.GenerateXmlAsync`.
7. Unit test: known `TaxReturn` → generate XML → validate against XSD.

## Notes

- XSD changes every tax year — generated classes must be regenerated annually.
- The interface insulates the rest of the system from schema changes.
- This task requires manual XSD download and may need adjustments per year.

## Expected Results

- `GenerateXmlAsync(taxReturn)` → valid EPO XML ready for upload
- XML validates against the official XSD schema
