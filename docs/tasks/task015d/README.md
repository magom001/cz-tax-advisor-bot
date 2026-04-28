# Task 015d — Output Bundle API & UI

## Objective

Wire all output generators together with API endpoints and download buttons in the web UI.

## Work Items

1. Implement `TaxReturnOutputService : ITaxReturnOutputService`:
   - Delegates to individual generators
   - `GenerateAllAsync` bundles XML + PDF + calculation table into a ZIP
2. Add API endpoints:
   - `GET /api/output/table?year=2025` — download calculation table PDF
   - `GET /api/output/xml?year=2025` — download EPO XML
   - `GET /api/output/pdf?year=2025` — download DPFO PDF
   - `GET /api/output/all?year=2025` — download ZIP with all artifacts
3. Add "Generate Output" section to web UI:
   - Show current tax return status
   - Download buttons for each artifact
   - Disable buttons if tax return is incomplete (use TaxValidationPlugin)
4. Register in DI.

## Dependencies

- Task 015a (calculation table)
- Task 015b (XML) — can be a stub initially
- Task 015c (PDF) — can be a stub initially

## Expected Results

- User clicks download → gets the artifact
- ZIP bundle contains all 3 files
- Buttons disabled when TaxReturn has missing fields
