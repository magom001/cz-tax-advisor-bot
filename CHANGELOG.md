# TaxAdvisorBot — Changelog

## Completed Tasks

### Task 001 — Solution Scaffolding & Aspire Setup
- Created solution with 10 projects: AppHost, ServiceDefaults, Domain, Application, Infrastructure, Web, CLI, 3 test projects
- .NET 10, Aspire with `Aspire.AppHost.Sdk`, Central Package Management
- `Directory.Build.props` for shared settings

### Task 002 — Domain Models & Enums
- `TaxReturn`, `StockTransaction`, `TaxDocumentContext`, `LegalReference`, `ChatResponse`, `ProgressUpdate`
- `TaxSection`, `FilingStatus`, `StockTransactionType`, `DocumentType` enums
- 3-year exemption rule (`IsExemptFromTax`), ESPP discount calculation
- 37 unit tests

### Task 003 — Application Interfaces & IOptions Configuration
- 7 service interfaces: `ITaxCalculationService`, `IDocumentExtractionService`, `ILegalSearchService`, `IConversationService`, `IExchangeRateService`, `INotificationService`, `IJobQueue`
- `ILegalIngestionService` for RAG ingestion
- 3 repository interfaces: `IUniformRateRepository`, `IConversationRepository`, `ITaxReturnRepository`
- IOptions: `AzureAIOptions` (6 deployment names), `QdrantOptions`, `LegalSourcesOptions`
- `ApplicationServiceRegistration` with `ValidateDataAnnotations().ValidateOnStart()`
- 13 options validation tests

### Task 004 — Semantic Kernel & Tax Calculation Plugins
- `TaxCalculationPlugin`: 5 `[KernelFunction]` methods (§6 tax base, §10 tax, income tax with 23% solidarity, §15 deductions with caps, §35ba/§35c credits)
- `TaxValidationPlugin`: `GetMissingFields` — checks personal details, employment, stock transactions, foreign income, child benefit
- `ExchangeRatePlugin`: 3 functions for ČNB rate conversion
- `SemanticKernelRegistration`: Kernel factory with 3 AI services (chat/fast-chat/reasoning), 10-min HttpClient timeout
- 31 infrastructure tests

### Task 005 — Qdrant Integration & Legal Search
- `QdrantLegalSearchService`: hybrid vector + keyword search with `effective_year` filtering and 0.65 score threshold
- `EmbeddingService`: Azure AI embedding wrapper via `IEmbeddingGenerator`
- `LegalTextSearchAdapter`: bridges `ILegalSearchService` to SK's `ITextSearch` for `TextSearchProvider`
- Qdrant in Aspire with persistent volume + `WaitFor`

### Task 005.1 — Legal Text Ingestion (LLM-based)
- `LegalIngestionService`: scrape URL → `ContentExtractor` → streaming LLM chunking → embed → store in Qdrant
- `BatchLegalIngestionService`: Azure OpenAI Batch API alternative (50% cheaper)
- `ContentExtractor`: HTML via HtmlAgilityPack DOM parser + PDF via PdfPig
- Legal sources configurable in `appsettings.json` (`LegalSourcesOptions`)
- Web UI: source list with per-source Ingest + Ingest All + Batch API buttons
- Collection reset before re-ingestion

### Task 006 — ČNB Exchange Rate Service
- `CnbExchangeRateService`: daily rates from `api.cnb.cz`, Redis caching (30-day TTL), JPY normalization
- `UniformRateSeeder`: seeds §38 rates from `appsettings.json` → MongoDB on startup
- 10 unit tests with mocked HTTP and fake cache/repository

### Infrastructure — MongoDB
- Added MongoDB to Aspire with persistent volume
- Repository pattern: `MongoUniformRateRepository`, `MongoConversationRepository`, `MongoTaxReturnRepository`
- `MongoCollections` for typed collection access
- Web API endpoints for rates management (`GET/POST /api/rates`)
- Rates management UI section

### Infrastructure — Redis
- Redis in Aspire for distributed caching
- `IDistributedCache` used by `CnbExchangeRateService`

### Agent — ChatCompletionAgent with RAG
- `TaxAdvisorAgentService`: implements `IConversationService` with streaming
- `ChatCompletionAgent` with `UseImmutableKernel`, `FunctionChoiceBehavior.Auto()`
- `TextSearchProvider` (BeforeAIInvoke) attached to `AgentThread.AIContextProviders`
- Conversation persistence in MongoDB via `IConversationRepository`
- SSE streaming chat endpoint (`GET /api/chat`)
- Chat UI section in web page

### Developer Experience
- Semantic Kernel skill (`.github/skills/semantic-kernel/`)
- `copilot-instructions.md` with full project conventions
- README with Azure AI setup, model deployment guide, user-secrets commands

## Test Summary
- **91 tests total**, all passing
  - Domain: 37 (models, computed properties, 3-year exemption)
  - Application: 13 (options validation)
  - Infrastructure: 41 (tax plugins, exchange rates)
