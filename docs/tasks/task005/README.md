# Task 005 — Qdrant Integration & Legal Search Service

## Objective

Set up Qdrant as the vector database via Aspire, implement the legal search service with hybrid search, and add Docker support.

## Work Items

1. Add Qdrant to AppHost via Aspire's `AddQdrant()`.
2. Create `docker/docker-compose.yml` with Qdrant service (fallback for non-Aspire runs).
3. Implement `QdrantLegalSearchService : ILegalSearchService`:
   - Vector similarity search using Azure AI embeddings.
   - Keyword filtering for exact § references.
   - Metadata filtering by `effective_year`.
   - Minimum score threshold — return "no relevant law found" below threshold.
4. Implement `EmbeddingService` — wraps Azure AI embedding model calls.
5. Add `QdrantOptions` registration in DI.
6. Write integration tests with Qdrant testcontainer:
   - Insert sample legal chunks, verify retrieval by semantic query.
   - Verify keyword filter finds exact § references.
   - Verify `effective_year` filter excludes wrong years.

## Expected Results

- `dotnet run --project src/TaxAdvisorBot.AppHost` starts Qdrant alongside the app.
- `QdrantLegalSearchService` returns relevant chunks for semantic queries.
- Exact § references (e.g., "§ 38f") are found via keyword filtering.
- Year filtering works correctly.
- Integration tests pass (require Docker).
