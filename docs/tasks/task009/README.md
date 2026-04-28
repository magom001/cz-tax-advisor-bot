# Task 009 — CLI Platform: REPL with Streaming

## Objective

Build the CLI REPL platform that provides an interactive tax advisor session in the terminal with streamed responses. Uses the same `IConversationService` (backed by the multi-agent system) as the Web platform.

## Work Items

1. Configure `Program.cs` with `Host.CreateApplicationBuilder`:
   - Register core services via `AddApplicationOptions()` + `AddInfrastructureServices()`.
   - Configure `user-secrets` for Azure AI keys.
2. Implement `ChatCommand`:
   - Interactive REPL loop: prompt → send to `IConversationService` → stream response via `IAsyncEnumerable<string>`.
   - Show session ID for continuity.
   - Support `exit` / `quit` / `new` (new session) commands.
3. Implement `ProcessFileCommand`:
   - Accept a file path, enqueue `DocumentUploadJob` via `IJobQueue`.
4. Implement `ConsoleNotificationService : INotificationService`.
5. Use `System.CommandLine` for command parsing.
6. Write unit tests.

## Expected Results

- `dotnet run --project src/platforms/TaxAdvisorBot.Cli -- chat` starts an interactive session.
- Responses stream token-by-token. Same agent orchestration as Web platform.
