# Task 011 — Legal Text Ingestion Tool

## Objective

Build the offline CLI tool that ingests Czech tax legislation, chunks it by § boundaries, generates embeddings, and stores them in Qdrant.

## Work Items

1. Create `src/tools/TaxAdvisorBot.Ingestion` console project.
2. Implement `ChunkingStrategy`:
   - Parse legal text (Markdown/plain text input).
   - Split by `§` paragraph boundaries.
   - Prepend section title to each chunk for context.
   - Generate metadata: `paragraph_id`, `sub_paragraph`, `effective_year`, `source_url`, `document_type`.
3. Implement `LegalTextIngestionPipeline`:
   - Read source files from a directory.
   - Apply chunking strategy.
   - Generate embeddings via `EmbeddingService`.
   - Upsert chunks + metadata into Qdrant.
4. Accept CLI arguments: input directory, target collection, effective year.
5. Write unit tests:
   - Chunking correctly splits sample legal text by §.
   - Metadata is correctly extracted from chunk headers.
   - Context injection (section title prepending) works.
6. Write integration test:
   - Ingest a small sample legal text file into Qdrant.
   - Verify chunks are retrievable via `ILegalSearchService`.

## Expected Results

- `dotnet run --project src/tools/TaxAdvisorBot.Ingestion -- --input ./data --collection czech-tax-2025 --year 2025` ingests legal texts.
- Chunks respect § boundaries — no paragraph is split mid-sentence.
- Each chunk has complete metadata for filtering.
- Unit tests pass without Qdrant (chunking logic only).
- Integration test passes with Qdrant (Docker required).
