# TaxAdvisorBot — Copilot Instructions

## Scope

- **Personal tax advisor for physical persons** (DPFO — daň z příjmů fyzických osob).
- Focus: yearly income tax return for an employed person with stock compensation.
- Key income types: §6 employment, §10 share sales (with 3-year exemption rule under §4 odst. 1 písm. w), RSU vesting, ESPP discount (10%).
- Key deductions (§15): pension fund, life insurance, mortgage interest, charitable donations.
- Key credits (§35ba/§35c): basic taxpayer, spouse, student, child tax benefit.
- **Output**: uploadable XML file (Czech Financial Administration EPO schema) + PDF form.

## Architecture

- Clean Architecture: Domain → Application → Infrastructure → Platforms.
- Business logic lives exclusively in `core/` projects (`Domain`, `Application`, `Infrastructure`). Platform projects (`Web`, `Telegram`, `Cli`) are thin shells — no domain logic.
- Multiple platforms share the same core: ASP.NET Web (SignalR), Telegram Bot, CLI REPL.
- See `docs/Architecture.md` for the full project structure and dependency flow.

## Code Style

- **Always code against interfaces, never implementations.** Services are consumed via `I*` interfaces injected through DI.
- **HostBuilder model with DI everywhere.** Every platform project uses `Host.CreateDefaultBuilder` or `WebApplication.CreateBuilder` and registers services through `IServiceCollection` extensions.
- **IOptions pattern for all configuration.** Every config section has a strongly-typed C# class with:
  - `[Required]`, `[Url]`, `[Range]`, and other `System.ComponentModel.DataAnnotations` attributes.
  - Registration via `.BindConfiguration(SectionName).ValidateDataAnnotations().ValidateOnStart()`.
- Use `sealed` classes by default unless inheritance is explicitly needed.
- Prefer `record` types for DTOs and value objects.
- Use `CancellationToken` on all async methods.

## AI & LLM

- AI models are deployed in **Azure AI Foundry**. Use Azure AI SDK / Semantic Kernel Azure connectors.
- Authentication: API keys (never hardcoded).
- Orchestration: **Semantic Kernel** with native C# plugins.
- **Never let the LLM do math.** All calculations happen in deterministic C# plugins.
- Every AI response must include citations (`LegalReference` with § number and source URL).

### Semantic Kernel Documentation

When implementing agent features, refer to these official docs:

- **Agents overview**: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-architecture
- **RAG (TextSearchProvider)**: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-rag?pivots=programming-language-csharp
- **Memory (Mem0 + Whiteboard)**: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-memory?pivots=programming-language-csharp
- **Agent orchestration (Sequential, Handoff, Concurrent, Group Chat)**: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/?pivots=programming-language-csharp
- **Text search concepts**: https://learn.microsoft.com/en-us/semantic-kernel/concepts/text-search/
- **Samples repo**: https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/Concepts/Agents

### Agent Architecture (target)

Use **Handoff orchestration** with specialized agents:
- **Legal Auditor** (gpt-4.1) — RAG search + Czech law interpretation. Uses `TextSearchProvider` with Qdrant.
- **Calculator** (plugins only) — Deterministic C# tax math via `TaxCalculationPlugin` and `ExchangeRatePlugin`.
- **Verifier** (o4-mini) — Cross-references calculation results against retrieved legal text.
- **Interviewer** (gpt-4.1-mini) — Conversational Q&A, asks user for missing information via `TaxValidationPlugin`.

Use `WhiteboardProvider` for short-term conversation memory (retains key facts across long sessions).
Use `IConversationRepository` (MongoDB) for persistent conversation history.

### Experimental Packages

```
dotnet add package Microsoft.SemanticKernel.Agents.Orchestration --prerelease
dotnet add package Microsoft.SemanticKernel.Agents.Runtime.InProcess --prerelease
```

## Secrets & Configuration

- **Local development**: `dotnet user-secrets` per platform project. Never commit secrets.
- **Aspire orchestration**: Environment variables injected by AppHost.
- **Production**: Azure Key Vault or environment variables.
- Configuration classes live in `Application/Options/`.

## .NET Aspire

- `TaxAdvisorBot.AppHost` is the Aspire orchestrator project.
- All infrastructure dependencies (Qdrant, Redis, message queues) are declared in AppHost.
- **Redis** is used for distributed caching (exchange rates, session data). Registered via `AddRedis("cache")` in AppHost, consumed via `IDistributedCache` / `AddRedisDistributedCache("cache")` in Infrastructure.
- Use `ServiceDefaults` for shared telemetry, health checks, and resilience.
- Docker is available for running dependencies locally (Qdrant, Redis, RabbitMQ, etc.).

## Real-Time & Async

- **UI must never freeze.** All long-running operations push progress updates to the client.
- Web platform: **SignalR** (WebSocket with fallback) for real-time chat and progress.
- Telegram: Bot API typing indicators + incremental message edits.
- CLI: `IAsyncEnumerable<string>` streamed to stdout.
- Use `INotificationService` abstraction — each platform provides its own implementation.
- **Pub/sub queues** for async jobs (document processing, vectorization, multi-step agent runs). Default: in-memory channels. Production: RabbitMQ or Azure Service Bus.

## RAG & Vector Search

- Vector DB: **Qdrant** (run locally via Docker, Aspire-managed).
- Chunking: split Czech legal text by `§` (paragraph) boundaries, never by character count.
- Hybrid search: vector similarity + keyword filtering.
- Always filter by `effective_year` metadata.

## Testing

- xUnit for all test projects.
- **Every new feature or service must include unit tests.** Do not consider a task complete without tests.
- Golden-case tax scenarios in integration tests.
- Mock interfaces, never concrete classes.
- Every C# tax calculation plugin must have unit tests.
- Use `[Fact]` for single cases, `[Theory]` with `[InlineData]` for parameterized scenarios.
- Integration tests requiring Docker or Azure are marked with `[Trait("Category", "Integration")]`.
- Test projects mirror the source structure: `TaxAdvisorBot.Domain.Tests`, `TaxAdvisorBot.Application.Tests`, `TaxAdvisorBot.Infrastructure.Tests`, `TaxAdvisorBot.Integration.Tests`.

## Conventions

- Async methods suffixed with `Async`.
- One class per file.
- Namespace matches folder structure.
- Use `Microsoft.Extensions.Logging.ILogger<T>` for logging — no static loggers.
- Structured logging with correlation IDs per conversation session.


## Semantic Kernel documentation

An Overview of the Agent Architecture: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-architecture?pivots=programming-language-csharp

The Semantic Kernel Common Agent API Surface: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-api?pivots=programming-language-csharp

Configuring Agents with Semantic Kernel Plugins: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-functions?pivots=programming-language-csharp

Contextual Function Selection with Agents: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-contextual-function-selection?pivots=programming-language-csharp

Create an Agent from a Semantic Kernel Template: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-templates?pivots=programming-language-csharp

How to Stream Agent Responses: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-streaming?pivots=programming-language-csharp

Using memory with Agents: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-memory?pivots=programming-language-csharp

Adding Retrieval Augmented Generation (RAG) to Semantic Kernel Agents: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-rag?pivots=programming-language-csharp

Exploring the Semantic Kernel AzureAIAgent: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-types/azure-ai-agent?pivots=programming-language-csharp

Exploring the Semantic Kernel BedrockAgent: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-types/bedrock-agent?pivots=programming-language-csharp

Exploring the Semantic Kernel ChatCompletionAgent: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-types/chat-completion-agent?pivots=programming-language-csharp

Exploring the Semantic Kernel CopilotStudioAgent: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-types/copilot-studio-agent?pivots=programming-language-csharp

Exploring the Semantic Kernel OpenAIAssistantAgent: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-types/assistant-agent?pivots=programming-language-csharp

Exploring the Semantic Kernel OpenAIResponsesAgent: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-types/responses-agent?pivots=programming-language-csharp

Semantic Kernel Agent Orchestration: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/?pivots=programming-language-csharp

Concurrent Orchestration: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/concurrent?pivots=programming-language-csharp

Sequential Orchestration: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/sequential?pivots=programming-language-csharp

Group Chat Orchestration: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/group-chat?pivots=programming-language-csharp

Handoff Orchestration: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/handoff?pivots=programming-language-csharp

Magentic Orchestration: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/magentic?pivots=programming-language-csharp

Semantic Kernel Agent Orchestration Advanced Topics: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/advanced-topics?pivots=programming-language-csharp

How-To: ChatCompletionAgent: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/examples/example-chat-agent?pivots=programming-language-csharp

How-To: OpenAIAssistantAgent Code Interpreter: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/examples/example-assistant-code?pivots=programming-language-csharp

How-To: OpenAIAssistantAgent File Search: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/examples/example-assistant-search?pivots=programming-language-csharp

Overview of the Process Framework: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/process-framework

Core Components of the Process Framework: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/process-core-components

Deployment of the Process Framework: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/process-deployment

Best Practices for the Process Framework: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/process-best-practices

How-To: Create your first Process: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/examples/example-first-process?pivots=programming-language-csharp

How-To: Using Cycles: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/examples/example-cycles?pivots=programming-language-csharp

How-To: Human-in-the-Loop: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/examples/example-human-in-loop?pivots=programming-language-csharp