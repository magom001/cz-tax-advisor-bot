# Task 011 — WhiteboardProvider & Agent Memory

## Status: Replaces original task (ingestion tool already built as LegalIngestionService)

## Objective

Add short-term conversation memory via `WhiteboardProvider` and long-term persistence via MongoDB. The Whiteboard retains key facts (income amounts, deductions claimed, children count) even when chat history is truncated.

## Work Items

1. Add `WhiteboardProvider` to the agent thread in the orchestration service.
2. Configure `WhiteboardProviderOptions`:
   - `MaxWhiteboardMessages`: 20 (retain most important facts)
   - Custom `ContextPrompt`: "The following facts have been established about the user's tax situation..."
3. Ensure Whiteboard state is populated from `IConversationRepository` on session resume.
4. Test that key facts survive across multiple turns even with history truncation.
5. Evaluate `Mem0Provider` for cross-session memory (user preferences, past filings) — implement if Mem0 service is available, otherwise defer.

## Expected Results

- User says "My income is 1.5M CZK, I have 2 children and a mortgage" → Whiteboard retains all 3 facts.
- 20 turns later, agent still knows the income, children, and mortgage without re-asking.
- Session resume from MongoDB restores the conversation context.
