# Task 015 — Observability, Logging & Audit Trail

## Objective

Add structured logging, correlation IDs, and an agent audit trail so every AI response can be traced back to its source data and reasoning steps.

## Work Items

1. Configure `ILogger<T>` across all services with structured logging.
2. Implement correlation ID middleware:
   - Generate a unique ID per conversation session.
   - Propagate through all log entries for that session.
   - Include in SignalR messages and API responses.
3. Implement agent audit trail logging:
   - Log every orchestrator step: user query → Qdrant query → retrieved chunks (with scores) → LLM prompt → LLM response → final answer.
   - Store as structured log entries (JSON) for later analysis.
4. Add health checks:
   - Qdrant connectivity.
   - Azure AI endpoint availability.
   - Register in ServiceDefaults.
5. Configure Aspire Dashboard integration for all telemetry.
6. Write unit tests:
   - Verify correlation ID is propagated through service calls.
   - Verify audit trail entries contain all required fields.

## Expected Results

- Every AI response can be traced to: which chunks were retrieved, what the LLM was asked, and what it returned.
- Correlation IDs tie together all log entries for a single user session.
- Health checks report status on the Aspire Dashboard.
- Structured logs are queryable by session ID.
- Unit tests pass.
