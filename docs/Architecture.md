# TaxAdvisorBot — Architecture

## 1. Overview

TaxAdvisorBot is a .NET-based AI tax advisor for Czech personal income tax (DPFO). It uses Retrieval-Augmented Generation (RAG) over Czech tax legislation, deterministic C# calculation plugins, and an agentic orchestration layer powered by Semantic Kernel.

The system follows a **"Verify-then-Calculate"** pipeline: retrieve relevant law → compute in C# → verify against source text.

### Design Principles

- **Business logic is platform-agnostic.** All domain logic lives in shared libraries. Platforms (Web, Telegram, CLI) are thin shells.
- **Code against interfaces, never implementations.** All services are registered and consumed via interfaces.
- **IOptions pattern everywhere.** Configuration uses strongly-typed models with data annotations and validation.
- **Non-blocking UI.** Long-running operations push progress updates via SignalR/WebSocket. The client never freezes waiting for a response.
- **Async jobs via pub/sub.** Document processing, vectorization, and multi-step agent workflows use message queues for decoupling.

---

## 2. Solution Structure

```
TaxAdvisorBot.sln
│
├── src/
│   │
│   ├── TaxAdvisorBot.AppHost/                  # .NET Aspire orchestrator
│   │   └── Program.cs                          # Wires up all services + dependencies (Qdrant, queues, etc.)
│   │
│   ├── TaxAdvisorBot.ServiceDefaults/          # Aspire shared service defaults (telemetry, health checks, resilience)
│   │
│   ├── core/                                   # ── Platform-agnostic business logic ──
│   │   │
│   │   ├── TaxAdvisorBot.Domain/               # Domain models, enums, value objects
│   │   │   ├── Models/
│   │   │   │   ├── TaxReturn.cs                # Structured state of a tax filing
│   │   │   │   ├── TaxDocumentContext.cs        # Extracted document fields
│   │   │   │   └── LegalReference.cs            # Citation model (§, paragraph, source URL)
│   │   │   └── Enums/
│   │   │       └── TaxSection.cs
│   │   │
│   │   ├── TaxAdvisorBot.Application/           # Use cases, interfaces, DTOs
│   │   │   ├── Interfaces/
│   │   │   │   ├── ITaxCalculationService.cs
│   │   │   │   ├── IDocumentExtractionService.cs
│   │   │   │   ├── ILegalSearchService.cs
│   │   │   │   ├── IConversationService.cs
│   │   │   │   ├── IExchangeRateService.cs
│   │   │   │   ├── INotificationService.cs      # Push progress updates to clients
│   │   │   │   └── IJobQueue.cs                 # Pub/sub abstraction
│   │   │   ├── DTOs/
│   │   │   ├── Options/                         # IOptions config models
│   │   │   │   ├── AzureAIOptions.cs            # Azure Foundry endpoints + model deployment names
│   │   │   │   ├── QdrantOptions.cs
│   │   │   │   └── DocumentIntelligenceOptions.cs
│   │   │   └── Validation/
│   │   │       └── TaxReturnValidator.cs        # Missing-field detection logic
│   │   │
│   │   └── TaxAdvisorBot.Infrastructure/        # Implementations of Application interfaces
│   │       ├── AI/
│   │       │   ├── SemanticKernelOrchestrator.cs # Kernel setup, plugin registration
│   │       │   └── Plugins/
│   │       │       ├── TaxCalculationPlugin.cs   # §6, §7, §8, §9, §10 calculations
│   │       │       ├── TaxValidationPlugin.cs    # GetMissingFields — deterministic C#
│   │       │       └── ExchangeRatePlugin.cs     # ČNB rate lookup
│   │       ├── Search/
│   │       │   ├── QdrantLegalSearchService.cs   # Hybrid search (vector + keyword)
│   │       │   └── EmbeddingService.cs           # Azure AI embeddings
│   │       ├── Documents/
│   │       │   └── AzureDocumentExtractionService.cs
│   │       ├── ExchangeRates/
│   │       │   └── CnbExchangeRateService.cs
│   │       ├── Messaging/
│   │       │   └── InMemoryJobQueue.cs           # Default impl; swappable for RabbitMQ/Azure Service Bus
│   │       └── DependencyInjection.cs            # IServiceCollection extensions
│   │
│   ├── platforms/                               # ── Thin platform shells ──
│   │   │
│   │   ├── TaxAdvisorBot.Web/                   # ASP.NET Core + Blazor / SignalR
│   │   │   ├── Hubs/
│   │   │   │   └── ChatHub.cs                   # SignalR hub for real-time chat + progress
│   │   │   ├── Controllers/
│   │   │   │   └── DocumentController.cs        # File upload endpoint
│   │   │   ├── wwwroot/
│   │   │   ├── Program.cs                       # HostBuilder, DI, middleware
│   │   │   └── appsettings.json
│   │   │
│   │   ├── TaxAdvisorBot.Telegram/              # Telegram Bot SDK
│   │   │   ├── Handlers/
│   │   │   │   ├── MessageHandler.cs
│   │   │   │   └── DocumentHandler.cs
│   │   │   ├── Program.cs                       # HostBuilder, DI
│   │   │   └── appsettings.json
│   │   │
│   │   └── TaxAdvisorBot.Cli/                   # Console REPL
│   │       ├── Commands/
│   │       │   ├── ChatCommand.cs
│   │       │   └── ProcessFileCommand.cs
│   │       ├── Program.cs                       # HostBuilder, DI
│   │       └── appsettings.json
│   │
│   └── tools/                                   # ── Offline / maintenance utilities ──
│       └── TaxAdvisorBot.Ingestion/             # CLI tool to ingest & vectorize legal texts
│           ├── Program.cs
│           └── Pipelines/
│               ├── LegalTextIngestionPipeline.cs
│               └── ChunkingStrategy.cs          # Split by § / article boundaries
│
├── tests/
│   ├── TaxAdvisorBot.Domain.Tests/
│   ├── TaxAdvisorBot.Application.Tests/
│   ├── TaxAdvisorBot.Infrastructure.Tests/
│   └── TaxAdvisorBot.Integration.Tests/         # Golden-case tax scenarios
│
├── docs/
│   ├── premise.md
│   └── Architecture.md                          # ← this file
│
└── docker/
    └── docker-compose.yml                       # Qdrant + any other local dependencies
```

---

## 3. Dependency Flow

```
Platforms (Web, Telegram, CLI)
       │
       ▼
  Application  (interfaces, DTOs, options)
       │
       ▼
  Infrastructure  (implementations: Semantic Kernel, Qdrant, Azure AI, ČNB)
       │
       ▼
    Domain  (models, enums — zero dependencies)
```

- **Domain** depends on nothing.
- **Application** depends on Domain.
- **Infrastructure** depends on Application + Domain (implements the interfaces).
- **Platforms** depend on Application (consume interfaces via DI). They do **not** reference Infrastructure directly — registration happens through `IServiceCollection` extensions.

---

## 4. Configuration & Secrets

| Environment | Mechanism |
|---|---|
| Local development | `dotnet user-secrets` per platform project |
| Aspire orchestration | Environment variables injected by AppHost |
| Production | Azure Key Vault / environment variables |

All configuration sections use the **IOptions pattern**:

```csharp
public class AzureAIOptions
{
    public const string SectionName = "AzureAI";

    [Required, Url]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string ChatDeploymentName { get; set; } = string.Empty;

    [Required]
    public string EmbeddingDeploymentName { get; set; } = string.Empty;
}
```

Registration:
```csharp
services.AddOptions<AzureAIOptions>()
    .BindConfiguration(AzureAIOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

## 5. .NET Aspire Orchestration

`TaxAdvisorBot.AppHost` defines the distributed application:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure dependencies
var qdrant = builder.AddQdrant("qdrant");

// Core API / worker
var api = builder.AddProject<Projects.TaxAdvisorBot_Web>("web")
    .WithReference(qdrant);

builder.Build().Run();
```

Aspire provides:
- **Service discovery** — platforms find Qdrant, queues, etc. by name.
- **Health checks & telemetry** — via `ServiceDefaults`.
- **Dashboard** — local observability for all services.

---

## 6. Real-Time Communication

All platforms push progress updates for long-running operations:

| Platform | Transport | Mechanism |
|---|---|---|
| Web | SignalR (WebSocket with fallback) | `ChatHub` pushes agent step updates, typing indicators, citations |
| Telegram | Telegram Bot API | `sendChatAction("typing")` + incremental message edits |
| CLI | Console streaming | `IAsyncEnumerable<string>` streamed to stdout |

The core `INotificationService` interface abstracts this:

```csharp
public interface INotificationService
{
    Task SendProgressAsync(string sessionId, ProgressUpdate update);
    Task SendCompletionAsync(string sessionId, ChatResponse response);
}
```

Each platform provides its own implementation.

---

## 7. Async Job Processing

Long-running tasks (document extraction, full vectorization, multi-step agent runs) are dispatched through `IJobQueue`:

```
Client → Platform → IJobQueue.EnqueueAsync(job)
                          │
                          ▼
                    Worker picks up job
                          │
                          ▼
              INotificationService.SendProgressAsync(...)
                          │
                          ▼
              INotificationService.SendCompletionAsync(...)
```

Default implementation: in-memory channel (sufficient for single-instance dev).  
Production: swap to RabbitMQ, Azure Service Bus, or Redis Streams via DI registration.

---

## 8. RAG Pipeline

### Ingestion (offline — `TaxAdvisorBot.Ingestion`)

1. **Extract** — Azure AI Document Intelligence (Layout model) → Markdown/JSON.
2. **Chunk** — Split by `§` paragraph boundaries. Prepend section title for context.
3. **Embed** — Azure AI embeddings (`text-embedding-3-small` or equivalent deployment).
4. **Store** — Upsert into Qdrant with metadata: `paragraph_id`, `effective_year`, `source_url`, `document_type`.

### Retrieval (runtime — `QdrantLegalSearchService`)

- **Hybrid search**: vector similarity + keyword filtering.
- **Metadata filter**: always scope to the relevant `effective_year`.
- **Minimum score threshold**: discard low-confidence results; surface "no relevant law found" rather than hallucinate.

---

## 9. Agentic Pipeline

Single orchestrator agent with native C# plugins (start simple, graduate to multi-agent only when needed):

```
User query
    │
    ▼
Orchestrator (Semantic Kernel ChatCompletionAgent)
    ├── calls LegalSearchPlugin     → retrieves § from Qdrant
    ├── calls TaxCalculationPlugin  → deterministic C# math
    ├── calls TaxValidationPlugin   → checks missing fields
    ├── calls ExchangeRatePlugin    → ČNB rates
    └── returns response with citations
```

- **LLM**: Azure AI (GPT-4o or equivalent deployment in Azure Foundry).
- **Math**: Always in C# plugins. The LLM is never trusted with arithmetic.
- **Citations**: Every response includes `LegalReference` objects linking to specific `§` and source.

---

## 10. Security & PII

- **Secrets**: Never in source. `dotnet user-secrets` locally, Key Vault in production.
- **PII scrubbing**: Sensitive fields (rodné číslo, bank accounts) are stripped before any LLM API call.
- **Data at rest**: Qdrant storage volume encrypted at the OS/container level.
- **Transport**: All API calls over HTTPS.
- **File uploads**: Scanned for size/type constraints before processing. Stored temporarily, deleted after extraction.

---

## 11. Observability

- **Aspire Dashboard**: Local telemetry for all services.
- **Structured logging**: `ILogger<T>` with correlation IDs per conversation session.
- **Agent audit trail**: Every orchestrator step logs: query → retrieved chunks (with scores) → LLM prompt → LLM response → final answer.
- **Health checks**: Qdrant connectivity, Azure AI endpoint availability.

---

## 12. Testing Strategy

| Layer | What | Tool |
|---|---|---|
| Domain | Value object invariants, model validation | xUnit |
| Application | Validation logic, missing-field detection | xUnit + mocks |
| Infrastructure | Plugin calculations, search accuracy | xUnit + Qdrant testcontainer |
| Integration | "Golden case" end-to-end tax scenarios | xUnit + WebApplicationFactory |

Golden cases: a suite of known tax scenarios (§10 RSU income, §6 employment, foreign dividends) with expected outputs, run on every CI build.
