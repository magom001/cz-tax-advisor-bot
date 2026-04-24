# Task 012 — Document Extraction Service (Azure AI Document Intelligence)

## Objective

Implement the document extraction service that uses Azure AI Document Intelligence to extract structured data from uploaded tax documents (invoices, tax forms, bank statements).

## Work Items

1. Add NuGet package: `Azure.AI.FormRecognizer`.
2. Implement `AzureDocumentExtractionService : IDocumentExtractionService`:
   - Accept file stream + content type.
   - Call Azure Document Intelligence (Layout or prebuilt model).
   - Map extracted fields to `TaxDocumentContext` model.
   - Return structured data with confidence scores.
3. Create `DocumentExtractionJob` for async processing via `IJobQueue`:
   - Enqueue when file is uploaded.
   - Process in background.
   - Notify client via `INotificationService` when complete.
4. Add `DocumentIntelligenceOptions` registration.
5. Write unit tests:
   - Verify field mapping from AI response to `TaxDocumentContext`.
   - Verify low-confidence fields are flagged for human review.
6. Write integration test (requires Azure AI Document Intelligence):
   - Process a sample invoice PDF.
   - Verify extracted amounts match expected values.

## Expected Results

- Uploaded documents are processed asynchronously.
- Extracted data populates the `TaxDocumentContext` with correct field values.
- Low-confidence extractions are flagged, not silently accepted.
- Progress updates are sent to the client during processing.
- Unit tests pass without Azure (mocked API responses).
