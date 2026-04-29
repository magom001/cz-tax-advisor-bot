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

### Task 007 — Async Job Queue & Notification Service
- `InMemoryJobQueue` using `System.Threading.Channels` with typed `JobEnvelope`
- `JobProcessorService` (`BackgroundService`) with `IJobHandler<T>` dispatch via reflection
- Scoped per job for fresh DI instances
- 4 unit tests (enqueue/dequeue, blocking, cancellation, ordering)

### Task 008 — Web Platform: SignalR Chat Hub & File Upload
- `ChatHub` (`/hubs/chat`) with `IAsyncEnumerable<string>` streaming via `SendMessage`
- `SignalRNotificationService : INotificationService` — broadcasts progress/completion via SignalR
- Multi-file upload endpoint (`POST /api/documents/upload`) — validates type/size per file, enqueues each via `IJobQueue`
- File drop zone in web UI with drag-and-drop + click-to-upload (multiple files)
- SSE streaming chat endpoint kept as fallback

### Task 011 — WhiteboardProvider & Agent Memory
- `WhiteboardProvider` integrated into `TaxAdvisorAgentService`
- `MaxWhiteboardMessages = 20`, custom `ContextPrompt` for tax-specific facts
- Uses `IChatClient` from Kernel's chat completion service
- Both `TextSearchProvider` and `WhiteboardProvider` are required (non-optional) dependencies
- Agent retains key facts (income, deductions, children) across conversation turns

### Task 012 — Document Extraction (LLM-based)
- `DocumentExtractionJobHandler`: PdfPig text extraction → gpt-4.1-mini structured JSON parsing
- Extracts RSU vesting, ESPP purchases, share sales, dividends, tax withheld
- Fetches ČNB daily exchange rate per transaction via `IExchangeRateService`
- Tax year detection from statement period dates (not hardcoded)
- ESPP: maps "Purchase Price" → `esppPurchasePrice`, "Fair Market Value" → `pricePerShare`
- Filters out internal adjustments, money market fund activity, unknown tickers
- Saves to `ITaxReturnRepository` (MongoDB), merges with existing year data

### Task 015a — Stock Calculation Table (QuestPDF)
- `StockCalculationTableGenerator`: landscape A4 PDF with RSU, ESPP, sales, dividends, tax withheld sections
- Per-transaction rows with daily ČNB rate and CZK amounts
- Summary section with §6/§8/§10 totals
- `TaxReturnOutputService` wiring + download endpoint

### Task 015d — Output Bundle API & UI
- `GET /api/output/table?year=` → PDF download
- `GET /api/output/all?year=` → ZIP bundle
- Green "Generate Output" section in web UI with year selector

### Dividend & Tax Withheld Support
- Added `Dividend` and `TaxWithheld` to `StockTransactionType` enum
- Added `GrossAmount` field to `StockTransaction` for amount-based transactions
- Extraction handler now persists `GrossAmount` from LLM output
- PDF table generator includes dividend and tax withheld sections
- TaxReturnPlugin exposes `GrossAmount` and `TaxWithheldAbroad` to agents
- 2 new domain tests (97 total)

### Debug Page
- `/debug.html` — dark-themed data inspection page
- Year selector, full tax return summary, income sections, deductions/credits
- Stock transactions grouped by type (RSU/ESPP/Sale/Dividend/TaxWithheld) with color-coded tags
- Per-transaction exchange rates, CZK amounts, missing data highlighting
- Totals summary with computed dividend/tax withheld amounts
- Raw JSON toggle, Delete Year button
- API: `GET /api/taxreturns`, `GET /api/taxreturns/{year}`, `DELETE /api/taxreturns/{year}`

### Task 010 — Multi-Agent Architecture
- Split monolithic agent into 3 specialized agents with keyword-based routing:
  - **Triage**: general Q&A, data checks, conversation steering. Plugins: TaxReturn, TaxValidation. Gets RAG.
  - **StockBroker**: RSU/ESPP/dividends/sales taxable base computation. Plugins: TaxReturn, ExchangeRate. No RAG.
  - **LegalAuditor**: Czech tax law Q&A via RAG. No plugins.
- `AgentDefinitions.cs`: centralized prompts + `Route()` keyword matcher
- Per-agent kernel cloning with selective plugin assignment
- StockBroker computes taxable base only (not final tax): ESPP net gain, 3-year exemption, §38f dividend credit method

## Test Summary
- **97 tests total**, all passing
  - Domain: 39 (models, computed properties, 3-year exemption, dividends)
  - Application: 13 (options validation)
  - Infrastructure: 45 (tax plugins, exchange rates, job queue)
