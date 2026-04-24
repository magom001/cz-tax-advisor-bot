# Task 013 — Telegram Bot Platform

## Objective

Build the Telegram Bot platform as a thin shell that uses the same core services as Web and CLI.

## Work Items

1. Create `src/platforms/TaxAdvisorBot.Telegram` project.
2. Add NuGet package: `Telegram.Bot`.
3. Configure `Program.cs` with `Host.CreateDefaultBuilder`:
   - Register core services via `AddInfrastructureServices()`.
   - Register Telegram Bot client.
   - Configure `user-secrets` for bot token + Azure AI keys.
4. Implement `MessageHandler`:
   - Receive text messages → send to `IConversationService`.
   - Stream response back as Telegram message (edit message for incremental updates).
   - Send `sendChatAction("typing")` during processing.
5. Implement `DocumentHandler`:
   - Receive document uploads from Telegram.
   - Enqueue extraction job.
   - Notify user when processing is complete.
6. Implement `TelegramNotificationService : INotificationService`:
   - Maps progress updates to Telegram message edits.
7. Register in AppHost.
8. Write unit tests:
   - Verify message handler invokes `IConversationService`.
   - Verify document handler enqueues extraction job.
   - Verify notification service formats messages correctly.

## Expected Results

- Telegram bot responds to messages with AI-generated tax advice.
- Documents sent to the bot are processed and extracted.
- Typing indicators appear during processing.
- Unit tests pass with mocked Telegram API.
