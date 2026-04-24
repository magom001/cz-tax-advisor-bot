# Task 010 — Conversation Service & AI Orchestration

## Objective

Implement the conversation service that ties together the Semantic Kernel orchestrator, legal search, tax calculation plugins, and multi-turn chat management.

## Work Items

1. Implement `ConversationService : IConversationService`:
   - Manages per-session chat history.
   - Sends user message to Semantic Kernel with registered plugins.
   - Returns `IAsyncEnumerable<string>` for streaming responses.
   - Extracts `LegalReference` citations from the AI response.
2. Configure the Kernel system prompt:
   - Instructs the model to use plugins for calculations and legal lookups.
   - Instructs the model to always cite sources with § numbers.
   - Instructs the model to call `GetMissingFields` after file uploads.
3. Implement session management:
   - In-memory dictionary of session ID → chat history.
   - Correlation ID logging per session.
4. Wire up all plugins: `TaxCalculationPlugin`, `TaxValidationPlugin`, `ExchangeRatePlugin`, `LegalSearchPlugin`.
5. Write unit tests:
   - Verify system prompt includes required instructions.
   - Verify session isolation (two sessions don't share history).
6. Write integration test (requires Azure AI):
   - Send a tax question, verify response includes citations.

## Expected Results

- End-to-end: user asks a tax question → AI retrieves law → calls C# plugin → returns answer with citations.
- Multi-turn context is maintained per session.
- Streaming works via `IAsyncEnumerable<string>`.
- Sessions are isolated.
- Unit tests pass without Azure AI (mocked kernel).
