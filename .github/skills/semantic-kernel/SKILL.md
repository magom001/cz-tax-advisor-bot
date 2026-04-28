---
name: semantic-kernel
description: 'Semantic Kernel Agent Framework reference for C#. Use when: implementing agents, orchestration (handoff, sequential, concurrent, group chat), RAG with TextSearchProvider, agent memory (Whiteboard, Mem0), plugins, function calling, streaming, ChatCompletionAgent, AzureAIAgent, OpenAIAssistantAgent, or any SK agent feature.'
---

# Semantic Kernel Agent Framework

## When to Use
- Implementing or modifying any Semantic Kernel agent (ChatCompletionAgent, AzureAIAgent, OpenAIAssistantAgent)
- Setting up agent orchestration (Handoff, Sequential, Concurrent, Group Chat, Magentic)
- Adding RAG via TextSearchProvider or TextSearchStore
- Configuring agent memory (WhiteboardProvider, Mem0Provider)
- Adding plugins or function calling to agents
- Implementing streaming agent responses
- Working with agent templates (YAML or inline)
- Using contextual function selection
- Structured input/output for orchestrations

## Procedure
1. Read [semantic-kernel-reference.md](./semantic-kernel-reference.md) before implementing any Semantic Kernel feature.
2. Follow the code patterns in the reference — they are distilled from official docs and tailored for this project's architecture.
3. For the TaxAdvisorBot, use **Handoff orchestration** (Section 9c) as the primary pattern.
4. Always set `UseImmutableKernel = true` when using RAG or contextual function selection.
5. Always enable `FunctionChoiceBehavior.Auto()` for ChatCompletionAgent when using plugins.
6. Use `InProcessRuntime` for orchestration execution.
