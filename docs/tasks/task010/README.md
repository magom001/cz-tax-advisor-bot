# Task 010 — Multi-Agent Handoff Orchestration

## Status: Replaces original task (conversation service already implemented as single-agent)

## Objective

Upgrade from single `ChatCompletionAgent` to **Handoff orchestration** with specialized agents, each with a dedicated role and model.

## Architecture

```
User message
    │
    ▼
Interviewer (gpt-4.1-mini) ← entry point, conversational
    │
    ├── handoff to → Legal Auditor (gpt-4.1) — RAG search + law interpretation
    ├── handoff to → Calculator (plugins) — deterministic tax math
    └── handoff to → Verifier (o4-mini) — cross-references results against law
```

## Work Items

1. Add packages: `Microsoft.SemanticKernel.Agents.Orchestration --prerelease`, `Microsoft.SemanticKernel.Agents.Runtime.InProcess --prerelease`.
2. Define 4 `ChatCompletionAgent` instances, each with:
   - Own system prompt and personality
   - Own model via `serviceId` (chat / fast-chat / reasoning)
   - Own subset of plugins
3. Create `HandoffOrchestration` with:
   - `StartWith(interviewerAgent)`
   - Handoff rules: Interviewer ↔ LegalAuditor ↔ Calculator → Verifier → Interviewer
   - `InteractiveCallback` for getting user input (bridges to SignalR/CLI/Telegram)
   - `ResponseCallback` for pushing responses to the client via `INotificationService`
4. Add `WhiteboardProvider` for retaining key facts across long conversations.
5. Integrate with `IConversationRepository` for persistent history.
6. Replace the current `TaxAdvisorAgentService` (single-agent) with the orchestrated version.
7. Write tests for handoff routing logic.

## Agents

| Agent | Model | Plugins | Role |
|---|---|---|---|
| **Interviewer** | gpt-4.1-mini | TaxValidation | Entry point. Greets, asks questions, collects data. |
| **Legal Auditor** | gpt-4.1 | (RAG via TextSearchProvider) | Searches Czech law, interprets §, produces citations. |
| **Calculator** | gpt-4.1-mini | TaxCalculation, ExchangeRate | Runs deterministic math, converts currencies. |
| **Verifier** | o4-mini | (RAG via TextSearchProvider) | Cross-references calculation results against law text. |

## Expected Results

- User starts a conversation → Interviewer asks for data.
- When legal question arises → handoff to Legal Auditor → cites specific §.
- When calculation needed → handoff to Calculator → exact CZK amounts.
- After calculation → handoff to Verifier → confirms correctness.
- Verifier hands back to Interviewer → presents result to user.
- WhiteboardProvider retains key facts (income, deductions, children) across turns.
