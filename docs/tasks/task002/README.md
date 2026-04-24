# Task 002 — Domain Models & Enums

## Objective

Define the core domain models, value objects, and enums that represent the tax filing domain. These have zero external dependencies.

## Work Items

1. Create `TaxReturn` model — structured state of a tax filing with fields for each income section (§6–§10), deductions, personal details, and filing status.
2. Create `LegalReference` record — citation model with `ParagraphId`, `SubParagraph`, `SourceUrl`, `Description`.
3. Create `TaxDocumentContext` model — extracted document fields (income amounts, dates, document type).
4. Create `TaxSection` enum — values for §6 (Employment), §7 (Self-employment), §8 (Capital gains), §9 (Rental), §10 (Other).
5. Create `FilingStatus` enum — Draft, MissingInfo, ReadyForReview, Complete.
6. Create `ProgressUpdate` record — for real-time notification payloads (step name, percentage, message).
7. Create `ChatResponse` record — AI response with answer text, list of `LegalReference` citations, confidence score.
8. Write unit tests for any domain validation logic (e.g., `TaxReturn` field constraints).

## Expected Results

- All models compile with no external NuGet dependencies.
- `record` types used for DTOs/value objects; `sealed class` for mutable state.
- Unit tests pass in `TaxAdvisorBot.Domain.Tests`.
- Models are documented with XML comments describing each field's purpose.
