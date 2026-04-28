# TaxAdvisorBot — Architecture

## 1. Overview

TaxAdvisorBot is a .NET 10 AI-powered personal tax advisor for Czech income tax (DPFO). It helps employed persons with stock compensation (RSU, ESPP, share sales) prepare their yearly income tax return, producing an uploadable XML file and PDF form.

Key technologies: Semantic Kernel (AI orchestration), Qdrant (vector search), MongoDB (persistent storage), Redis (caching), .NET Aspire (orchestration), Azure AI Foundry (LLM models).

### Design Principles

- **Business logic is platform-agnostic.** All domain logic lives in `core/` projects. Platforms (Web, Telegram, CLI) are thin shells.
- **Code against interfaces, never implementations.** All services consumed via `I*` interfaces through DI.
- **Repository pattern for data access.** MongoDB repositories behind interfaces in Application layer.
- **IOptions pattern everywhere.** Config uses strongly-typed models with `[Required]`, `[Url]`, `[Range]` annotations and `ValidateOnStart()`.
- **Non-blocking UI.** Long-running operations (ingestion, LLM calls) run in background with polling/SSE for progress.
- **LLM never does math.** All tax calculations happen in deterministic C# plugins.

---

## 2. Solution Structure

```
TaxAdvisorBot.slnx
│
├── src/
│   ├── TaxAdvisorBot.AppHost/                     # .NET Aspire orchestrator
│   │   └── Program.cs                             # Redis, Qdrant, MongoDB, Web
│   │
│   ├── TaxAdvisorBot.ServiceDefaults/             # OpenTelemetry, health checks, resilience
│   │
│   ├── core/
│   │   ├── TaxAdvisorBot.Domain/                  # Zero dependencies
│   │   │   ├── Models/
│   │   │   │   ├── TaxReturn.cs                   # Mutable tax filing state
│   │   │   │   ├── StockTransaction.cs            # RSU/ESPP/share sale with 3-year exemption
│   │   │   │   ├── TaxDocumentContext.cs           # Extracted document fields
│   │   │   │   ├── LegalReference.cs               # Citation (§, URL)
│   │   │   │   ├── ChatResponse.cs                 # AI response + citations
│   │   │   │   └── ProgressUpdate.cs               # Real-time progress payload
│   │   │   └── Enums/
│   │   │       ├── TaxSection.cs                   # §6–§10
│   │   │       ├── FilingStatus.cs                 # Draft → Complete
│   │   │       ├── StockTransactionType.cs         # RsuVesting, EsppDiscount, ShareSale
│   │   │       └── DocumentType.cs                 # Employment, brokerage, pension, etc.
│   │   │
│   │   ├── TaxAdvisorBot.Application/             # Interfaces, DTOs, options
│   │   │   ├── Interfaces/                        # ← All documented in §4 below
│   │   │   │   ├── ITaxCalculationService.cs
│   │   │   │   ├── IDocumentExtractionService.cs
│   │   │   │   ├── ILegalSearchService.cs
│   │   │   │   ├── ILegalIngestionService.cs
│   │   │   │   ├── IConversationService.cs
│   │   │   │   ├── IExchangeRateService.cs
│   │   │   │   ├── INotificationService.cs
│   │   │   │   ├── IJobQueue.cs
│   │   │   │   ├── IUniformRateRepository.cs
│   │   │   │   ├── IConversationRepository.cs
│   │   │   │   └── ITaxReturnRepository.cs
│   │   │   ├── Options/
│   │   │   │   ├── AzureAIOptions.cs               # AI endpoint, deployments, API key
│   │   │   │   ├── QdrantOptions.cs                # Connection string, collection, vector size
│   │   │   │   └── LegalSourcesOptions.cs          # Legal source URLs for ingestion
│   │   │   └── ApplicationServiceRegistration.cs   # IOptions binding + validation
│   │   │
│   │   └── TaxAdvisorBot.Infrastructure/          # Implementations
│   │       ├── AI/
│   │       │   ├── SemanticKernelRegistration.cs   # Kernel factory with 3 AI services
│   │       │   └── Plugins/
│   │       │       ├── TaxCalculationPlugin.cs     # §6, §10 tax, deductions, credits
│   │       │       ├── TaxValidationPlugin.cs      # Missing-field detection
│   │       │       └── ExchangeRatePlugin.cs       # ČNB rate conversion
│   │       ├── Search/
│   │       │   ├── QdrantLegalSearchService.cs     # Hybrid vector + keyword search
│   │       │   ├── LegalIngestionService.cs        # Real-time LLM chunking + embedding
│   │       │   ├── BatchLegalIngestionService.cs   # Azure Batch API ingestion
│   │       │   ├── EmbeddingService.cs             # Azure AI embedding wrapper
│   │       │   └── ContentExtractor.cs             # HTML (HtmlAgilityPack) + PDF (PdfPig)
│   │       ├── ExchangeRates/
│   │       │   ├── CnbExchangeRateService.cs       # ČNB daily rates + Redis caching
│   │       │   └── UniformRateSeeder.cs            # Seeds §38 rates from appsettings
│   │       ├── Persistence/
│   │       │   ├── MongoCollections.cs              # Typed collection accessors + documents
│   │       │   ├── MongoUniformRateRepository.cs
│   │       │   ├── MongoConversationRepository.cs
│   │       │   └── MongoTaxReturnRepository.cs
│   │       └── DependencyInjection.cs              # Wires everything into IHostApplicationBuilder
│   │
│   └── platforms/
│       ├── TaxAdvisorBot.Web/                     # ASP.NET Core
│       │   ├── Program.cs                         # API endpoints (search, ingest, rates)
│       │   ├── wwwroot/index.html                 # Knowledge base UI + search
│       │   └── appsettings.json                   # Legal sources, uniform rates
│       │
│       └── TaxAdvisorBot.Cli/                     # Console app
│           └── Program.cs                         # HostBuilder + ServiceDefaults
│
├── tests/
│   ├── TaxAdvisorBot.Domain.Tests/                # 37 tests — models, computed properties
│   ├── TaxAdvisorBot.Application.Tests/           # 13 tests — options validation
│   └── TaxAdvisorBot.Infrastructure.Tests/        # 41 tests — plugins, exchange rates
│
└── docs/
    ├── premise.md
    └── Architecture.md                            # ← this file
```

---

## 3. Dependency Flow

```
Platforms (Web, CLI)
       │
       ▼
  Application  (interfaces, DTOs, options — no implementations)
       │
       ▼
  Infrastructure  (Semantic Kernel, Qdrant, MongoDB, ČNB, Azure AI)
       │
       ▼
    Domain  (models, enums — zero dependencies)
```

- **Domain** depends on nothing.
- **Application** depends on Domain. Defines interfaces and DTOs only.
- **Infrastructure** depends on Application + Domain. Implements all interfaces.
- **Platforms** reference Application + Infrastructure. Call `builder.AddApplicationOptions()` and `builder.AddInfrastructureServices()` to wire everything via DI.

---

## 4. Interfaces (Application Layer)

### Service Interfaces

| Interface | Purpose | Implementation |
|---|---|---|
| `ITaxCalculationService` | Deterministic tax math — §6/§10 income, deductions, credits. Returns `TaxCalculationResult` with amount + citations. | `TaxCalculationPlugin` (Semantic Kernel) |
| `IDocumentExtractionService` | Extracts structured data from uploaded documents (PDF, images) via LLM. Returns `TaxDocumentContext` with confidence scores. | LLM-based (gpt-4.1-mini) |
| `ILegalSearchService` | Hybrid vector + keyword search over Czech tax law in Qdrant. Filters by `effective_year`. Returns ranked `LegalSearchResult` list. | `QdrantLegalSearchService` |
| `ILegalIngestionService` | Scrapes legal text from URLs, chunks by § using LLM, embeds, stores in Qdrant. Supports `ResetCollectionAsync` for re-ingestion. | `LegalIngestionService` (streaming) |
| `IConversationService` | Multi-turn AI chat. Streams responses via `IAsyncEnumerable<string>`. Maintains per-session history. | (planned) |
| `IExchangeRateService` | ČNB daily exchange rates (API) + §38 uniform yearly rates (manual config). Caches in Redis. | `CnbExchangeRateService` |
| `INotificationService` | Pushes `ProgressUpdate` and `ChatResponse` to connected clients. Platform-specific implementations. | (per platform) |
| `IJobQueue` | Generic pub/sub for async background processing. `EnqueueAsync<T>` / `DequeueAsync<T>`. | In-memory channels (default) |

### Repository Interfaces

| Interface | Purpose | Implementation |
|---|---|---|
| `IUniformRateRepository` | CRUD for §38 uniform yearly exchange rates. `GetRateAsync(year, currency)`, `SetRateAsync`, `GetAllAsync`. | `MongoUniformRateRepository` |
| `IConversationRepository` | Conversation history persistence. `GetAsync(sessionId)`, `AddMessageAsync`, `SaveAsync`, `DeleteAsync`. | `MongoConversationRepository` |
| `ITaxReturnRepository` | Tax return state persistence. `GetAsync(id)`, `GetByYearAsync`, `SaveAsync`, `GetAllAsync`. | `MongoTaxReturnRepository` |

---

## 5. Configuration (IOptions)

| Options Class | Config Section | Key Fields |
|---|---|---|
| `AzureAIOptions` | `AzureAI` | `Endpoint` [Required, Url], `ApiKey` [Required], `ChatDeploymentName` (gpt-4.1), `FastChatDeploymentName` (gpt-4.1-mini), `ReasoningDeploymentName` (o4-mini, optional), `BatchDeploymentName` (optional), `BatchEndpoint` (optional), `EmbeddingDeploymentName` |
| `QdrantOptions` | `Qdrant` | `ConnectionString` [Required] (injected by Aspire), `CollectionName` (default: czech-tax), `VectorSize` (default: 1536) |
| `LegalSourcesOptions` | `LegalSources` | `Sources[]` — list of `LegalSource` { Name, Url, DocumentType, Description } |

Uniform exchange rates are stored in `appsettings.json` under `UniformRates` and seeded into MongoDB on startup:
```json
"UniformRates": { "2024:USD": 23.14, "2025:USD": 23.48 }
```

---

## 6. .NET Aspire Orchestration

```csharp
var redis = builder.AddRedis("cache");

var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume("qdrant-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongo = builder.AddMongoDB("mongodb")
    .WithDataVolume("mongo-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongoDB = mongo.AddDatabase("taxadvisor");

var web = builder.AddCSharpApp("web", "...")
    .WithReference(redis)
    .WithReference(qdrant)
    .WithReference(mongoDB);
```

| Resource | Purpose | Persistent |
|---|---|---|
| **Redis** | Distributed cache (exchange rates, session data) | No (ephemeral cache) |
| **Qdrant** | Vector DB for RAG search over Czech tax law | Yes (Docker volume) |
| **MongoDB** | Conversations, tax returns, uniform rates | Yes (Docker volume) |

---

## 7. Semantic Kernel Plugins

Three AI services are registered with the Kernel:

| Service ID | Deployment | Use |
|---|---|---|
| `chat` | gpt-4.1 | Legal analysis, complex reasoning, citation generation |
| `fast-chat` | gpt-4.1-mini | Data extraction, ingestion chunking, simple Q&A |
| `reasoning` | o4-mini | Multi-step tax verification (optional) |

Three native C# plugins:

| Plugin | Functions | Description |
|---|---|---|
| `TaxCalculation` | `CalculateSection6TaxBase`, `CalculateSection10Tax`, `CalculateIncomeTax`, `ApplyDeductions`, `ApplyCredits` | Deterministic tax math — 15% base rate, 23% solidarity surcharge, §15 deduction caps, §35ba/§35c credits |
| `TaxValidation` | `GetMissingFields` | Checks `TaxReturn` for incomplete data — personal details, employment consistency, stock transactions, foreign income |
| `ExchangeRate` | `ConvertToCzkAsync`, `GetDailyRateAsync`, `GetUniformRateAsync` | ČNB exchange rates for foreign income conversion |

---

## 8. RAG Pipeline

### Ingestion

Two modes:

1. **Real-time** (`LegalIngestionService`) — per-source, streaming LLM calls via `GetStreamingChatMessageContentsAsync`. Used by "Ingest All" button.
2. **Batch** (`BatchLegalIngestionService`) — all sources in one JSONL file via Azure OpenAI Batch API (50% cheaper). Requires GlobalBatch deployment.

Pipeline: Scrape URL → `ContentExtractor` (HTML via HtmlAgilityPack / PDF via PdfPig) → split into 30k-char batches → LLM extracts § paragraphs as JSON → embed via `text-embedding-ada-002` → upsert into Qdrant with metadata.

Legal sources are configured in `appsettings.json` under `LegalSources:Sources[]`.

### Retrieval (`QdrantLegalSearchService`)

- **Hybrid search**: vector similarity + keyword filtering for exact § references
- **Metadata filter**: always scope by `effective_year`
- **Minimum score threshold** (0.65): discard low-confidence results

---

## 9. Persistence (MongoDB)

Repository pattern — interfaces in Application, MongoDB implementations in Infrastructure.

| Collection | Document | Repository | Purpose |
|---|---|---|---|
| `uniform_rates` | `UniformRateDocument` | `MongoUniformRateRepository` | §38 yearly exchange rates |
| `conversations` | `ConversationDocument` | `MongoConversationRepository` | Chat history with atomic message append |
| `tax_returns` | `TaxReturnDocument` | `MongoTaxReturnRepository` | Full `TaxReturn` as BSON document |

Seeded from `appsettings.json` on startup via `UniformRateSeeder` (`IHostedService`).

---

## 10. Web API Endpoints

| Method | Path | Description |
|---|---|---|
| GET | `/api/sources` | List configured legal sources from appsettings |
| GET | `/api/search?q=...&year=2025` | Search the RAG knowledge base |
| POST | `/api/ingest` | Start background ingestion for a single source |
| GET | `/api/ingest/{jobId}` | Poll ingestion job status |
| POST | `/api/ingest/reset` | Wipe and recreate the Qdrant collection |
| POST | `/api/batch-ingest` | Start batch ingestion of all sources |
| GET | `/api/batch-ingest/status` | Poll batch ingestion progress |
| GET | `/api/rates` | List all uniform exchange rates |
| POST | `/api/rates` | Set a uniform exchange rate |

---

## 11. Security & PII

- **Secrets**: `dotnet user-secrets` locally, Key Vault in production. Never in source.
- **PII**: Sensitive fields (rodné číslo, bank accounts) to be stripped before LLM API calls.
- **Transport**: All API calls over HTTPS.
- **File uploads**: Size/type constraints enforced before processing.

---

## 12. Testing Strategy

| Layer | Tests | Coverage |
|---|---|---|
| Domain (37) | `TaxReturn` computed properties, `StockTransaction` 3-year exemption, record equality | Models, enums |
| Application (13) | Options validation — missing fields, invalid URLs, range constraints | IOptions |
| Infrastructure (41) | Tax calculation plugin (income tax, deductions, credits), validation plugin, ČNB exchange rates (mocked HTTP + cache) | Plugins, services |

Total: **91 tests**, all passing.

Golden cases: `TaxAdvisorBot.Integration.Tests` (planned) — known tax scenarios with expected outputs.
