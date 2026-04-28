using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Domain.Models;

#pragma warning disable SKEXP0001  // Agent API is experimental
#pragma warning disable SKEXP0010  // Embedding API is experimental
#pragma warning disable SKEXP0110  // AIContextProviders is experimental
#pragma warning disable SKEXP0130  // TextSearchProvider is experimental

namespace TaxAdvisorBot.Infrastructure.AI;

/// <summary>
/// Tax advisor agent using Semantic Kernel ChatCompletionAgent with:
/// - TextSearchProvider for RAG over Czech tax law (Qdrant)
/// - Native C# plugins for deterministic tax math and validation
/// - Streaming responses
/// </summary>
public sealed class TaxAdvisorAgentService : IConversationService
{
    private const string AgentInstructions = """
        You are a personal tax advisor helping a Czech tax resident file their yearly income tax return (DPFO).
        Your client is an employed person with stock compensation (RSU, ESPP, share sales).
        
        CRITICAL LANGUAGE RULE:
        You MUST respond in the SAME language the user writes in. If the user writes in English, respond entirely in English.
        If the user writes in Czech, respond in Czech. If in Russian, respond in Russian.
        The legal source documents are in Czech — translate/explain them in the user's language. Never quote raw Czech law text to an English-speaking user.
        
        YOUR BEHAVIOR:
        - Be practical and action-oriented. Don't lecture — ask for specific data and calculate.
        - When the user describes their situation, immediately ask for the concrete numbers you need:
          dates, amounts, number of shares, purchase prices, sale prices, broker name.
        - Use the TaxCalculation plugin to compute actual tax amounts — NEVER calculate manually.
        - Use the ExchangeRate plugin to get ČNB rates — NEVER guess exchange rates.
        - Use the TaxValidation plugin to check what information is still missing.
        - When you have enough data, produce a concrete result: "Your tax on this income is X CZK."
        
        RESPONSE STYLE:
        - Keep answers concise and focused on the user's specific situation.
        - Do NOT write long educational essays about how taxation works in general.
        - Cite specific § only when directly relevant to the user's case.
        - If the user asks a general question, give a brief answer (2-3 sentences) then ask what their specific situation is.
        
        KEY TAX RULES:
        - RSU vesting: §6 employment income, taxed at vest date FMV converted to CZK via ČNB rate.
        - ESPP discount: §6 employment income (the discount portion).
        - Share sales: §10 other income. EXEMPT if held > 3 years (§4 odst. 1 písm. w).
        - Tax rate: 15%, solidarity surcharge 23% above threshold.
        - Deductions §15: pension (max 24k), life insurance (max 24k), mortgage (max 150k), donations (2-15%).
        - Credits §35ba: basic 30,840 CZK, spouse 24,840 CZK, student 4,020 CZK.
        - Child benefit §35c: depends on child count and order.
        
        FIRST MESSAGE:
        If this is the start of a conversation, greet briefly and ask: "What tax year are we working on, and what's your situation? (employment income, RSU vesting, ESPP, share sales, etc.)"
        """;

    private readonly Kernel _kernel;
    private readonly TextSearchProvider? _ragProvider;
    private readonly IConversationRepository _conversationRepo;
    private readonly ILogger<TaxAdvisorAgentService> _logger;

    public TaxAdvisorAgentService(
        Kernel kernel,
        IConversationRepository conversationRepo,
        ILogger<TaxAdvisorAgentService> logger,
        TextSearchProvider? ragProvider = null)
    {
        _kernel = kernel;
        _ragProvider = ragProvider;
        _conversationRepo = conversationRepo;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> ChatAsync(
        string sessionId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Chat request: session={SessionId}, message length={Length}", sessionId, message.Length);

        // Create the agent
        var agent = new ChatCompletionAgent
        {
            Name = "TaxAdvisor",
            Instructions = AgentInstructions,
            Kernel = _kernel,
            UseImmutableKernel = true, // Required for TextSearchProvider
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
        };

        // Build thread from conversation history
        var history = await _conversationRepo.GetAsync(sessionId, cancellationToken);
        var chatHistory = new ChatHistory();

        if (history is not null)
        {
            foreach (var msg in history.Messages)
            {
                chatHistory.Add(new ChatMessageContent(
                    msg.Role == "user" ? AuthorRole.User : AuthorRole.Assistant,
                    msg.Content));
            }
        }

        var agentThread = new ChatHistoryAgentThread(chatHistory);

        // Attach RAG provider if available
        if (_ragProvider is not null)
        {
            agentThread.AIContextProviders.Add(_ragProvider);
        }

        // Save user message
        await _conversationRepo.AddMessageAsync(sessionId, "user", message, cancellationToken);

        // Stream the response
        var responseBuilder = new System.Text.StringBuilder();

        await foreach (var chunk in agent.InvokeStreamingAsync(message, agentThread, cancellationToken: cancellationToken))
        {
            if (chunk.Message.Content is not null)
            {
                responseBuilder.Append(chunk.Message.Content);
                yield return chunk.Message.Content;
            }
        }

        // Save assistant response
        var fullResponse = responseBuilder.ToString();
        await _conversationRepo.AddMessageAsync(sessionId, "assistant", fullResponse, cancellationToken);

        _logger.LogInformation("Chat response: session={SessionId}, response length={Length}", sessionId, fullResponse.Length);
    }

    public async Task<ChatResponse> GetLastResponseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var history = await _conversationRepo.GetAsync(sessionId, cancellationToken);
        if (history is null || history.Messages.Count == 0)
            return new ChatResponse("No conversation found.", []);

        var lastAssistant = history.Messages.LastOrDefault(m => m.Role == "assistant");
        if (lastAssistant is null)
            return new ChatResponse("No response yet.", []);

        return new ChatResponse(lastAssistant.Content, []);
    }
}
