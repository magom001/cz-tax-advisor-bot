using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Memory;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Domain.Models;
using TaxAdvisorBot.Infrastructure.AI.Agents;

#pragma warning disable SKEXP0001  // Agent API is experimental
#pragma warning disable SKEXP0010  // Embedding API is experimental
#pragma warning disable SKEXP0110  // AIContextProviders is experimental
#pragma warning disable SKEXP0120  // WhiteboardProvider is experimental
#pragma warning disable SKEXP0130  // TextSearchProvider is experimental

namespace TaxAdvisorBot.Infrastructure.AI;

/// <summary>
/// Multi-agent tax advisor using specialized agents routed by topic:
/// - Triage: general Q&amp;A, conversation steering
/// - StockBroker: RSU/ESPP/dividends/sales calculations
/// - LegalAuditor: Czech tax law questions (RAG)
/// Each agent gets only the plugins it needs.
/// </summary>
public sealed class TaxAdvisorAgentService : IConversationService
{
    private readonly Kernel _kernel;
    private readonly TextSearchProvider _ragProvider;
    private readonly WhiteboardProvider _whiteboardProvider;
    private readonly IConversationRepository _conversationRepo;
    private readonly ILogger<TaxAdvisorAgentService> _logger;

    public TaxAdvisorAgentService(
        Kernel kernel,
        TextSearchProvider ragProvider,
        WhiteboardProvider whiteboardProvider,
        IConversationRepository conversationRepo,
        ILogger<TaxAdvisorAgentService> logger)
    {
        _kernel = kernel;
        _ragProvider = ragProvider;
        _whiteboardProvider = whiteboardProvider;
        _conversationRepo = conversationRepo;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> ChatAsync(
        string sessionId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Route to the right specialist
        var route = AgentDefinitions.Route(message);
        _logger.LogInformation("Chat request: session={SessionId}, route={Route}, message length={Length}",
            sessionId, route, message.Length);

        var agent = CreateAgent(route);

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

        // Attach AI context providers based on agent type
        if (route is AgentRoute.Triage or AgentRoute.LegalAuditor or AgentRoute.PersonalFinance)
            agentThread.AIContextProviders.Add(_ragProvider);

        agentThread.AIContextProviders.Add(_whiteboardProvider);

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

        _logger.LogInformation("Chat response: session={SessionId}, route={Route}, response length={Length}",
            sessionId, route, fullResponse.Length);
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

    private ChatCompletionAgent CreateAgent(AgentRoute route)
    {
        return route switch
        {
            AgentRoute.StockBroker => new ChatCompletionAgent
            {
                Name = "StockBroker",
                Description = "Stock compensation tax specialist — RSU, ESPP, share sales, dividends, foreign tax withheld",
                Instructions = AgentDefinitions.StockBrokerInstructions,
                Kernel = CreateKernelForRoute(route),
                UseImmutableKernel = true,
                Arguments = new KernelArguments(
                    new OpenAIPromptExecutionSettings
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    })
            },
            AgentRoute.LegalAuditor => new ChatCompletionAgent
            {
                Name = "LegalAuditor",
                Description = "Czech tax law specialist — answers legal questions using RAG over Czech tax law",
                Instructions = AgentDefinitions.LegalAuditorInstructions,
                Kernel = CreateKernelForRoute(route),
                UseImmutableKernel = true,
                Arguments = new KernelArguments(
                    new OpenAIPromptExecutionSettings
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    })
            },
            AgentRoute.PersonalFinance => new ChatCompletionAgent
            {
                Name = "PersonalFinance",
                Description = "Personal finance specialist — deductions (§15), credits (§35ba/§35c), employment income (§6), personal data",
                Instructions = AgentDefinitions.PersonalFinanceInstructions,
                Kernel = CreateKernelForRoute(route),
                UseImmutableKernel = true,
                Arguments = new KernelArguments(
                    new OpenAIPromptExecutionSettings
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    })
            },
            _ => new ChatCompletionAgent
            {
                Name = "Triage",
                Description = "General tax advisor — routes to specialists, handles general questions",
                Instructions = AgentDefinitions.TriageInstructions,
                Kernel = CreateKernelForRoute(route),
                UseImmutableKernel = true,
                Arguments = new KernelArguments(
                    new OpenAIPromptExecutionSettings
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    })
            },
        };
    }

    /// <summary>
    /// Creates a kernel clone with only the plugins needed for the specific agent route.
    /// </summary>
    private Kernel CreateKernelForRoute(AgentRoute route)
    {
        // Clone the kernel so each agent gets its own plugin set
        var kernel = _kernel.Clone();

        // Remove all plugins — we'll add back only what's needed
        var pluginNames = kernel.Plugins.Select(p => p.Name).ToList();
        foreach (var name in pluginNames)
            kernel.Plugins.Remove(kernel.Plugins[name]);

        switch (route)
        {
            case AgentRoute.StockBroker:
                // Stock broker needs: TaxReturn (read data), ExchangeRate (rates)
                kernel.Plugins.Add(_kernel.Plugins["TaxReturn"]);
                kernel.Plugins.Add(_kernel.Plugins["ExchangeRate"]);
                break;

            case AgentRoute.LegalAuditor:
                // Legal auditor: no plugins — uses RAG via TextSearchProvider on the thread
                break;

            case AgentRoute.PersonalFinance:
                // Personal finance needs: TaxReturn, TaxCalculation (deductions/credits), TaxValidation
                kernel.Plugins.Add(_kernel.Plugins["TaxReturn"]);
                kernel.Plugins.Add(_kernel.Plugins["TaxCalculation"]);
                kernel.Plugins.Add(_kernel.Plugins["TaxValidation"]);
                break;

            case AgentRoute.Triage:
                // Triage needs: TaxReturn (check what data exists), TaxValidation (what's missing)
                kernel.Plugins.Add(_kernel.Plugins["TaxReturn"]);
                kernel.Plugins.Add(_kernel.Plugins["TaxValidation"]);
                break;
        }

        return kernel;
    }
}
