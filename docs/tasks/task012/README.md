# Task 012 — User Document Extraction (LLM-based)

## Objective

When the user **uploads their personal tax documents** (Fidelity brokerage statement, employment confirmation, pension fund statement, etc.), extract the relevant numbers and populate the `TaxReturn` model automatically.

This is NOT about RAG/Qdrant (that's legal text for the knowledge base). This is about the user's own documents — "here's my Fidelity PDF, fill in my tax return."

## Work Items

1. Create `DocumentExtractionJobHandler : IJobHandler<DocumentUploadJob>`:
   - Receives uploaded file from the job queue (enqueued by `/api/documents/upload`).
   - Uses `ContentExtractor` to get text from PDF/image/spreadsheet.
   - Sends extracted text to gpt-4.1-mini with a structured extraction prompt.
   - Parses LLM response into `StockTransaction[]` or `TaxReturn` field updates.
   - Saves to `ITaxReturnRepository`.
   - Notifies client via `INotificationService`.
2. Extraction prompts per document type:
   - **Brokerage statement** → RSU vests, ESPP purchases, dividends, tax withheld (dates, amounts, prices)
   - **Employment confirmation** (Potvrzení) → §6 gross income, social/health insurance, tax advances
   - **Pension/insurance statement** → §15 deduction amounts
   - **Mortgage interest confirmation** → §15 mortgage interest
3. Flag low-confidence extractions for user confirmation via the chat.
4. Write unit tests with sample document text.

## Expected Results

- User drops a Fidelity PDF → system extracts all stock transactions.
- User drops "Potvrzení o zdanitelných příjmech" → §6 fields auto-populated.
- User is asked to confirm any ambiguous extractions.
