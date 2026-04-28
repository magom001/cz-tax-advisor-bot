# Semantic Kernel Agent Framework — C# Reference

> Distilled from official Microsoft docs (April 2026). C#-only, focused on patterns used in TaxAdvisorBot.

## Packages

```
dotnet add package Microsoft.SemanticKernel.Agents.Core --prerelease
dotnet add package Microsoft.SemanticKernel.Agents.OpenAI --prerelease
dotnet add package Microsoft.SemanticKernel.Agents.AzureAI --prerelease
dotnet add package Microsoft.SemanticKernel.Agents.Orchestration --prerelease
dotnet add package Microsoft.SemanticKernel.Agents.Runtime.InProcess --prerelease
```

---

## 1. Agent Types

### ChatCompletionAgent (primary for TaxAdvisorBot)

```csharp
IKernelBuilder builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion("<deployment>", "<endpoint>", "<api-key>");
Kernel kernel = builder.Build();

ChatCompletionAgent agent = new()
{
    Name = "TaxCalculator",
    Instructions = "You are a tax calculation agent...",
    Kernel = kernel,
    // Required when using ContextualFunctionProvider or OnDemandFunctionCalling RAG
    UseImmutableKernel = true
};
```

**Enable function calling** (must be explicit for ChatCompletionAgent):
```csharp
ChatCompletionAgent agent = new()
{
    Name = "MyAgent",
    Instructions = "...",
    Kernel = kernel,
    Arguments = new KernelArguments(
        new OpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        })
};
```

**Multiple AI services / service selection:**
```csharp
builder.AddAzureOpenAIChatCompletion(/*...*/, serviceId: "gpt4");
builder.AddAzureOpenAIChatCompletion(/*...*/, serviceId: "gpt4-mini");

ChatCompletionAgent agent = new()
{
    Kernel = kernel,
    Arguments = new KernelArguments(
        new OpenAIPromptExecutionSettings { ServiceId = "gpt4" })
};
```

### OpenAIAssistantAgent

```csharp
AssistantClient client = OpenAIAssistantAgent.CreateAzureOpenAIClient(
    new AzureCliCredential(), new Uri("<endpoint>")).GetAssistantClient();

Assistant assistant = await client.CreateAssistantAsync(
    "<model>", "<name>", instructions: "<instructions>");

OpenAIAssistantAgent agent = new(assistant, client);
```

Thread management:
```csharp
OpenAIAssistantAgentThread agentThread = new(client);
// or resume: new OpenAIAssistantAgentThread(client, "existing-thread-id");
// cleanup:
await agentThread.DeleteAsync();
await client.DeleteAssistantAsync("<assistant-id>");
```

### AzureAIAgent

```csharp
PersistentAgentsClient client = AzureAIAgent.CreateAgentsClient("<endpoint>", new AzureCliCredential());

PersistentAgent definition = await client.Administration.CreateAgentAsync(
    "<model>", name: "<name>", instructions: "<instructions>");

AzureAIAgent agent = new(definition, client);
// Thread: AzureAIAgentThread agentThread = new(agent.Client);
```

---

## 2. Common Invocation API

All agent types share the same invocation interface:

```csharp
// Non-streaming
await foreach (AgentResponseItem<ChatMessageContent> response
    in agent.InvokeAsync("user input", agentThread))
{
    Console.WriteLine(response.Message.Content);
    agentThread = response.Thread; // capture thread if new
}

// Streaming
await foreach (StreamingChatMessageContent response
    in agent.InvokeStreamingAsync("user input", agentThread))
{
    Console.Write(response.Content);
}

// Without thread (creates new one automatically)
var result = await agent.InvokeAsync("What is X?").FirstAsync();
var newThread = result.Thread;

// With options
agent.InvokeAsync("input", agentThread, options: new()
{
    AdditionalInstructions = "Extra context for this call only",
    KernelArguments = overrideArgs,
    OnIntermediateMessage = msg => { /* function call/result messages */ }
});
```

**AgentThread lifecycle:**
```csharp
// Create
ChatHistoryAgentThread thread = new();
// or with existing history: new ChatHistoryAgentThread(existingChatHistory);

// Delete
await agentThread.DeleteAsync();
```

---

## 3. Plugins & Function Calling

```csharp
// Import plugin from type
kernel.ImportPluginFromType<TaxCalculationPlugin>();

// Import plugin from object instance
kernel.ImportPluginFromObject(new ExchangeRatePlugin(httpClient));

// Create plugin from individual functions
var func = kernel.CreateFunctionFromMethod(MyStaticMethod);
var promptFunc = kernel.CreateFunctionFromPrompt("Summarize: {{$input}}");
kernel.ImportPluginFromFunctions("myPlugin", [func, promptFunc]);

// Clone kernel for agent-specific plugins
Kernel agentKernel = kernel.Clone();
agentKernel.ImportPluginFromType<SpecialPlugin>();
```

**Key rule:** OpenAIAssistantAgent always uses automatic function calling. ChatCompletionAgent requires explicit `FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()`.

---

## 4. Agent Templates

### Inline template parameters
```csharp
ChatCompletionAgent agent = new(
    templateFactory: new KernelPromptTemplateFactory(),
    templateConfig: new("Analyze tax for {{$year}} with income {{$income}}")
    {
        TemplateFormat = PromptTemplateConfig.SemanticKernelTemplateFormat
    })
{
    Kernel = kernel,
    Name = "TaxAnalyzer",
    Arguments = new KernelArguments()
    {
        { "year", "2024" },
        { "income", "500000" },
    }
};
```

### YAML template definition
```yaml
name: TaxAnalyzer
template: |
  Analyze the tax situation for year {{$year}}.
  Income: {{$income}} CZK
template_format: semantic-kernel
description: Analyzes tax for a given year and income.
input_variables:
  - name: year
    description: Tax year
    is_required: true
  - name: income
    description: Total income in CZK
    is_required: true
```

```csharp
string yaml = File.ReadAllText("./TaxAnalyzer.yaml");
PromptTemplateConfig config = KernelFunctionYaml.ToPromptTemplateConfig(yaml);
ChatCompletionAgent agent = new(config) { Kernel = kernel };
```

---

## 5. Streaming

```csharp
// ChatCompletionAgent streaming
await foreach (StreamingChatMessageContent response
    in agent.InvokeStreamingAsync(message, agentThread))
{
    Console.Write(response.Content);
}

// Read full messages after streaming completes
await foreach (ChatMessageContent msg in agentThread.GetMessagesAsync())
{
    Console.WriteLine(msg.Content);
}
```

**Key types:** `StreamingChatMessageContent`, `StreamingTextContent`, `StreamingFileReferenceContent`, `StreamingAnnotationContent`

---

## 6. RAG — TextSearchProvider

### Basic setup with TextSearchStore + InMemoryVectorStore
```csharp
var embeddingGenerator = new AzureOpenAIClient(
    new Uri("<endpoint>"), new AzureCliCredential())
    .GetEmbeddingClient("<deployment>")
    .AsIEmbeddingGenerator(1536);

var vectorStore = new InMemoryVectorStore(
    new() { EmbeddingGenerator = embeddingGenerator });

using var textSearchStore = new TextSearchStore<string>(
    vectorStore, collectionName: "LegalText", vectorDimensions: 1536);

// Upsert documents
await textSearchStore.UpsertTextAsync(new[]
{
    "§6 Income from employment includes...",
    "§10 Other income includes capital gains..."
});

// Create agent with RAG
ChatCompletionAgent agent = new()
{
    Name = "LegalAuditor",
    Instructions = "You search Czech tax law and provide legal interpretations.",
    Kernel = kernel,
    UseImmutableKernel = true  // REQUIRED for RAG
};

// Attach to thread
ChatHistoryAgentThread agentThread = new();
var textSearchProvider = new TextSearchProvider(textSearchStore);
agentThread.AIContextProviders.Add(textSearchProvider);
```

### Citations
```csharp
await textSearchStore.UpsertDocumentsAsync(new[]
{
    new TextSearchDocument
    {
        Text = "§4 odst. 1 písm. w) exempts share sales held > 3 years...",
        SourceName = "Zákon č. 586/1992 Sb. §4",
        SourceLink = "https://www.zakonyprolidi.cz/cs/1992-586#p4-1-w",
        Namespaces = ["tax_law/2024"]
    }
});
```

### Namespace filtering (e.g., by effective_year)
```csharp
using var textSearchStore = new TextSearchStore<string>(
    vectorStore, collectionName: "LegalText", vectorDimensions: 1536,
    new() { SearchNamespace = "tax_law/2024" });
```

### Automatic vs On-Demand RAG
```csharp
// Default: BeforeAIInvoke — searches before each invocation
// On-demand: agent decides when to search via tool call
var options = new TextSearchProviderOptions
{
    SearchTime = TextSearchProviderOptions.RagBehavior.OnDemandFunctionCalling,
    Top = 5,
    PluginFunctionName = "SearchLegalText",
    PluginFunctionDescription = "Search Czech tax law paragraphs"
};

var provider = new TextSearchProvider(textSearchStore, options: options);
```

### TextSearchProvider options
- **Top** (default: 3): Max results from similarity search
- **SearchTime**: `BeforeAIInvoke` (default) or `OnDemandFunctionCalling`
- **PluginFunctionName/Description**: Customize the tool call name for on-demand
- **ContextPrompt**: Override default prompt explaining what retrieved chunks are
- **IncludeCitationsPrompt**: Override citation instructions
- **ContextFormatter**: Completely custom output formatting callback

---

## 7. Memory

### WhiteboardProvider (short-term, in-conversation)

Extracts requirements, proposals, decisions, actions from conversation. Ideal for tax filing sessions.

```csharp
var whiteboardProvider = new WhiteboardProvider(chatClient);

ChatHistoryAgentThread agentThread = new();
agentThread.AIContextProviders.Add(whiteboardProvider);

await agent.InvokeAsync("My income was 1.5M CZK, I have a mortgage.", agentThread);
// Whiteboard now retains: income=1.5M, has mortgage deduction
```

**Options (`WhiteboardProviderOptions`):**
- `MaxWhiteboardMessages`: Limit entries, removes less valuable ones
- `ContextPrompt`: Override what the whiteboard contents mean
- `WhiteboardEmptyPrompt`: Message when whiteboard is empty
- `MaintenancePromptTemplate`: Override the LLM prompt for whiteboard updates. Params: `{{$maxWhiteboardMessages}}`, `{{$inputMessages}}`, `{{$currentWhiteboard}}`

### Mem0Provider (long-term, cross-session)

```csharp
using var httpClient = new HttpClient()
{
    BaseAddress = new Uri("https://api.mem0.ai")
};
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Token", "<api-key>");

var mem0Provider = new Mem0Provider(httpClient, options: new()
{
    UserId = "user-123",
    // ScopeToPerOperationThreadId = true  // use agent thread id
});

agentThread.AIContextProviders.Add(mem0Provider);
```

**Scoping:** `ApplicationId`, `AgentId`, `ThreadId`, `UserId`

### Combining providers
```csharp
agentThread.AIContextProviders.Add(mem0Provider);
agentThread.AIContextProviders.Add(whiteboardProvider);
agentThread.AIContextProviders.Add(textSearchProvider);
```

---

## 8. Contextual Function Selection

When agents have many plugins, use RAG to filter relevant functions per invocation:

```csharp
var contextualProvider = new ContextualFunctionProvider(
    vectorStore: new InMemoryVectorStore(
        new() { EmbeddingGenerator = embeddingGenerator }),
    vectorDimensions: 1536,
    functions: GetAllFunctions(), // IReadOnlyList<AIFunction>
    maxNumberOfFunctions: 5
);

agentThread.AIContextProviders.Add(contextualProvider);

// Agent MUST have UseImmutableKernel = true
```

**Options (`ContextualFunctionProviderOptions`):**
- `NumberOfRecentMessagesInContext` (default: 2)
- `ContextEmbeddingValueProvider`: Custom delegate to build context embedding
- `EmbeddingValueProvider`: Custom delegate for function embedding

---

## 9. Orchestration Patterns

All patterns share the same invocation flow:

```csharp
// 1. Create orchestration
var orchestration = new XxxOrchestration(agents...) { ResponseCallback = ... };

// 2. Start runtime
InProcessRuntime runtime = new();
await runtime.StartAsync();

// 3. Invoke
var result = await orchestration.InvokeAsync(task, runtime);

// 4. Get result
string output = await result.GetValueAsync(TimeSpan.FromSeconds(300));

// 5. Cleanup
await runtime.RunUntilIdleAsync();
```

### 9a. Sequential Orchestration

Pipeline: each agent processes the previous agent's output.

```csharp
SequentialOrchestration orchestration = new(analystAgent, writerAgent, editorAgent)
{
    ResponseCallback = responseCallback
};

var result = await orchestration.InvokeAsync("Analyze this tax scenario...", runtime);
string output = await result.GetValueAsync(TimeSpan.FromSeconds(60));
```

**Use for:** Extract → Calculate → Validate → Generate pipelines.

### 9b. Concurrent Orchestration

All agents process the same input in parallel; results collected as array.

```csharp
ConcurrentOrchestration orchestration = new(agent1, agent2, agent3);

var result = await orchestration.InvokeAsync("What is the tax rate?", runtime);
string[] outputs = await result.GetValueAsync(TimeSpan.FromSeconds(20));
```

**Use for:** Parallel rate lookups + law searches.

### 9c. Handoff Orchestration ⭐ (TaxAdvisorBot target pattern)

Dynamic agent switching based on context. Supports human-in-the-loop.

```csharp
// Define agents
ChatCompletionAgent triageAgent = new() { Name = "Interviewer", ... };
ChatCompletionAgent calcAgent = new() { Name = "Calculator", ... };
ChatCompletionAgent legalAgent = new() { Name = "LegalAuditor", ... };
ChatCompletionAgent verifierAgent = new() { Name = "Verifier", ... };

// Define handoff relationships
var handoffs = OrchestrationHandoffs
    .StartWith(triageAgent)
    .Add(triageAgent, calcAgent, legalAgent)
    .Add(calcAgent, verifierAgent, "Transfer after calculation is complete")
    .Add(legalAgent, calcAgent, "Transfer when legal question is resolved")
    .Add(verifierAgent, triageAgent, "Transfer when verification is complete");

// Human-in-the-loop
HandoffOrchestration orchestration = new(
    handoffs, triageAgent, calcAgent, legalAgent, verifierAgent)
{
    InteractiveCallback = async () =>
    {
        // Get input from user (SignalR, Telegram, CLI)
        string input = await GetUserInputAsync();
        return new ChatMessageContent(AuthorRole.User, input);
    },
    ResponseCallback = async msg =>
    {
        // Push to client (SignalR, Telegram, CLI)
        await notificationService.SendAsync(msg);
    }
};
```

### 9d. Group Chat Orchestration

Multi-agent conversation with a manager controlling turns.

```csharp
// Built-in round-robin manager
GroupChatOrchestration orchestration = new(
    new RoundRobinGroupChatManager { MaximumInvocationCount = 5 },
    writer, editor)
{
    ResponseCallback = responseCallback
};

// Custom manager (override abstract methods)
public class TaxReviewManager : GroupChatManager
{
    public override ValueTask<GroupChatManagerResult<string>> FilterResults(
        ChatHistory history, CancellationToken ct) { ... }

    public override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(
        ChatHistory history, GroupChatTeam team, CancellationToken ct) { ... }

    public override ValueTask<GroupChatManagerResult<bool>> ShouldTerminate(
        ChatHistory history, CancellationToken ct) { ... }

    public override ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInput(
        ChatHistory history, CancellationToken ct) { ... }
}
```

**Manager call order:** ShouldRequestUserInput → ShouldTerminate → FilterResults (if terminating) → SelectNextAgent (if continuing)

---

## 10. Advanced Orchestration Topics

### Structured Input/Output
```csharp
// Typed input
public sealed class TaxInput
{
    public decimal Income { get; set; }
    public int Year { get; set; }
    public string[] Deductions { get; set; } = [];
}

HandoffOrchestration<TaxInput, string> orchestration = new(...)
{
    InputTransform = (input, ct) =>
    {
        var msg = new ChatMessageContent(AuthorRole.User,
            $"Year: {input.Year}, Income: {input.Income}");
        return ValueTask.FromResult<IEnumerable<ChatMessageContent>>([msg]);
    }
};

var result = await orchestration.InvokeAsync(new TaxInput { ... }, runtime);
```

### Structured output
```csharp
public sealed class TaxResult
{
    public decimal TaxDue { get; set; }
    public IList<string> AppliedDeductions { get; set; } = [];
}

StructuredOutputTransform<TaxResult> outputTransform = new(
    chatCompletionService,
    new OpenAIPromptExecutionSettings { ResponseFormat = typeof(TaxResult) });

ConcurrentOrchestration<string, TaxResult> orchestration = new(agents...)
{
    ResultTransform = outputTransform.TransformAsync
};

OrchestrationResult<TaxResult> result = await orchestration.InvokeAsync(input, runtime);
TaxResult output = await result.GetValueAsync(TimeSpan.FromSeconds(60));
```

### Response callback
```csharp
ValueTask ResponseCallback(ChatMessageContent response)
{
    Console.WriteLine($"# {response.AuthorName}: {response.Content}");
    return ValueTask.CompletedTask;
}
```

### Timeouts & cancellation
```csharp
// Timeout on result retrieval (orchestration continues in background)
string output = await result.GetValueAsync(TimeSpan.FromSeconds(60));

// Cancel orchestration (stops further message processing)
var resultTask = orchestration.InvokeAsync(input, runtime);
resultTask.Cancel();
```

---

## 11. Key Gotchas

1. **`UseImmutableKernel = true`** is REQUIRED when using `TextSearchProvider` (on-demand mode), `ContextualFunctionProvider`, or any `AIContextProvider` that modifies the kernel. Kernel modifications by plugins will NOT be retained.

2. **`FunctionChoiceBehavior.Auto()`** must be explicitly set for `ChatCompletionAgent` to enable function calling. `OpenAIAssistantAgent` always uses auto.

3. **Thread types are agent-specific:** `AzureAIAgent` requires `AzureAIAgentThread`, `OpenAIAssistantAgent` requires `OpenAIAssistantAgentThread`. `ChatCompletionAgent` uses `ChatHistoryAgentThread`.

4. **Orchestration result is async.** `InvokeAsync` returns immediately. Call `GetValueAsync(timeout)` to wait for result. Timeout doesn't cancel the orchestration.

5. **Runtime must be started before invoking orchestration** and cleaned up with `RunUntilIdleAsync()`.

6. **Handoff agents need `Description`** — the orchestration uses it to decide which agent to route to.

7. **Multiple AIContextProviders can be combined** on the same thread (Mem0 + Whiteboard + RAG).

8. **In-memory vector stores** are recommended for `ContextualFunctionProvider` — external stores require manual sync on function list changes.

---

## 12. Sample Links

- [Agents concepts samples](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/Concepts/Agents)
- [Getting started with agents](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/GettingStartedWithAgents)
- [Orchestration samples](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/GettingStartedWithAgents/Orchestration)
- [RAG sample](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/Agents/ChatCompletion_Rag.cs)
- [Whiteboard sample](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/Agents/ChatCompletion_Whiteboard.cs)
- [Mem0 sample](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/Agents/ChatCompletion_Mem0.cs)
- [Contextual function selection](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/Agents/ChatCompletion_ContextualFunctionSelection.cs)
- [Handoff orchestration](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStartedWithAgents/Orchestration/Step04_Handoff.cs)
- [Handoff with structured input](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStartedWithAgents/Orchestration/Step04a_HandoffWithStructuredInput.cs)
- [Sequential orchestration](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStartedWithAgents/Orchestration/Step02_Sequential.cs)
- [Concurrent orchestration](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStartedWithAgents/Orchestration/Step01_Concurrent.cs)
- [Group chat orchestration](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStartedWithAgents/Orchestration/Step03_GroupChat.cs)
- [Group chat with AI manager](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStartedWithAgents/Orchestration/Step03b_GroupChatWithAIManager.cs)
