# Task 009 — CLI Platform: REPL with Streaming

## Objective

Build the CLI REPL platform that provides an interactive tax advisor session in the terminal with streamed responses.

## Work Items

1. Configure `Program.cs` with `Host.CreateDefaultBuilder`:
   - Register core services via `AddInfrastructureServices()`.
   - Configure `user-secrets` for Azure AI keys.
2. Implement `ChatCommand`:
   - Interactive REPL loop: prompt → send to `IConversationService` → stream response via `IAsyncEnumerable<string>`.
   - Display citations after each response.
   - Support `exit` / `quit` commands.
3. Implement `ProcessFileCommand`:
   - Accept a file path argument.
   - Enqueue document extraction job.
   - Display progress in console.
4. Implement `ConsoleNotificationService : INotificationService`:
   - Writes progress updates to stdout with spinners/progress bars.
5. Use `System.CommandLine` for command parsing.
6. Write unit tests:
   - Verify `ConsoleNotificationService` produces expected output.
   - Verify command parsing accepts valid arguments.

## Expected Results

- `dotnet run --project src/platforms/TaxAdvisorBot.Cli -- chat` starts an interactive session.
- Responses stream token-by-token to the terminal.
- `dotnet run --project src/platforms/TaxAdvisorBot.Cli -- process-file <path>` processes a document.
- Unit tests pass.
