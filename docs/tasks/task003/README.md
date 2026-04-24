# Task 003 — Application Interfaces & IOptions Configuration

## Objective

Define all service interfaces in the Application layer and create strongly-typed IOptions configuration models with data annotation validation.

## Work Items

### Interfaces

1. `ITaxCalculationService` — calculate tax for each section, return results with citations.
2. `IDocumentExtractionService` — extract structured data from uploaded documents.
3. `ILegalSearchService` — search the vector DB for relevant legal text.
4. `IConversationService` — manage multi-turn chat with the AI orchestrator.
5. `IExchangeRateService` — fetch ČNB exchange rates.
6. `INotificationService` — push progress updates and completions to clients.
7. `IJobQueue` — enqueue/dequeue async jobs.

### IOptions Models (in `Application/Options/`)

8. `AzureAIOptions` — Endpoint (Required, Url), ApiKey (Required), ChatDeploymentName, EmbeddingDeploymentName.
9. `QdrantOptions` — Endpoint (Required, Url), CollectionName, VectorSize.
10. `DocumentIntelligenceOptions` — Endpoint (Required, Url), ApiKey (Required).

### Registration

11. Create `IServiceCollection` extension method stubs for registering options with `ValidateDataAnnotations().ValidateOnStart()`.

### Tests

12. Write unit tests verifying that options models fail validation when required fields are missing.

## Expected Results

- All interfaces defined with `CancellationToken` on async methods.
- Options models have proper `[Required]`, `[Url]`, `[Range]` annotations.
- Validation tests confirm that invalid options are rejected.
- Application project has zero implementation code — only interfaces, DTOs, and options.
