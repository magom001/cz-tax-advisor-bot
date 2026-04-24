# Task 005.1 — Legal Text Ingestion (LLM-based)

## Objective

Populate the Qdrant knowledge database by scraping Czech tax law from the web, using LLM to chunk by § boundaries, generating embeddings, and storing with metadata.

## What was built

### LegalIngestionService

- Scrapes HTML from a given URL (e.g. zakonyprolidi.cz)
- Strips HTML tags to get raw text
- Sends text in batches to `gpt-4.1-mini` (fast-chat) to extract § paragraphs as structured JSON
- Each chunk has: `paragraphId`, `subParagraph`, `title`, `content`
- Generates embeddings via `text-embedding-ada-002`
- Stores in Qdrant with metadata: `paragraph_id`, `sub_paragraph`, `text_content`, `title`, `effective_year`, `document_type`, `source_url`
- Auto-creates the Qdrant collection if it doesn't exist

### Web UI

- `/api/ingest` POST endpoint — triggers ingestion from a URL
- `/api/search` GET endpoint — searches the knowledge base
- `wwwroot/index.html` — interactive page with:
  - Ingest section: paste URL + year, click Ingest
  - Search section: type query, see ranked results with relevance scores and source links

## How to use

1. Start Aspire: `dotnet run --project src/TaxAdvisorBot.AppHost`
2. Open the web app URL from the Aspire dashboard
3. Click "Ingest" with `https://zakonyprolidi.cz/cs/1992-586` and year `2025`
4. Wait for ingestion to complete (a few minutes)
5. Search for "RSU vesting tax" or "§ 10" to see results
