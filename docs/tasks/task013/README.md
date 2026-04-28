# Task 013 — Telegram Bot Platform

## Objective

Build the Telegram Bot platform as a thin shell using the same `IConversationService` and agent orchestration as Web and CLI.

## Work Items

1. Create `src/platforms/TaxAdvisorBot.Telegram` project.
2. Add NuGet package: `Telegram.Bot`.
3. Configure `Program.cs` with `Host.CreateApplicationBuilder`:
   - Register core services via `AddApplicationOptions()` + `AddInfrastructureServices()`.
   - Register Telegram Bot client via `user-secrets` (bot token).
4. Implement `MessageHandler`:
   - Receive text messages → `IConversationService.ChatAsync()`.
   - Stream response as Telegram message with incremental edits.
   - `sendChatAction("typing")` during processing.
5. Implement `DocumentHandler`:
   - Receive document uploads → enqueue `DocumentUploadJob`.
   - Notify when extraction completes.
6. Implement `TelegramNotificationService : INotificationService`.
7. Register in AppHost.
8. Write unit tests with mocked Telegram API.

## Expected Results

- Telegram bot responds with AI-generated tax advice using the same agent orchestration.
- Documents sent to bot are extracted and processed.
- Typing indicators during processing. Unit tests pass.
