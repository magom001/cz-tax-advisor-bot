# TaxAdvisorBot — Copilot Instructions

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

## Secrets & Configuration

- **Local development**: `dotnet user-secrets` per platform project. Never commit secrets.
- **Aspire orchestration**: Environment variables injected by AppHost.
- **Production**: Azure Key Vault or environment variables.
- Configuration classes live in `Application/Options/`.

## .NET Aspire

- `TaxAdvisorBot.AppHost` is the Aspire orchestrator project.
- All infrastructure dependencies (Qdrant, message queues) are declared in AppHost.
- Use `ServiceDefaults` for shared telemetry, health checks, and resilience.
- Docker is available for running dependencies locally (Qdrant, RabbitMQ, etc.).

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
