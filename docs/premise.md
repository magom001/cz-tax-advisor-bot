For a .NET-based tax advisor, you should use Semantic Kernel as your orchestration layer. It is the industry standard for integrating LLMs into .NET apps, offering a structured way to handle RAG, native C# plugins, and agentic workflows.

1. High-Level Pipeline (The "Tax Brain")
Your pipeline must follow a "Verify-then-Calculate" flow to prevent the AI from making up tax laws.

Ingestion & Vectorization:

Source Data: The Czech Income Tax Act (ZDP 586/1992), D-series instructions (Pokyny D-xx), and official ČNB exchange rates.

Vector DB: Use Qdrant or Pinecone (with .NET connectors). Use Azure AI Search if you want high-quality hybrid search (Vector + Keyword) out of the box.

Chunking Strategy: Don't just split by characters. Split by Paragraphs (§) or Articles. Legal logic is contained within the whole paragraph; cutting it in half destroys the meaning.

Document Extraction (The "Folder" Input):

Tool: Use Azure AI Document Intelligence (formerly Form Recognizer). It has pre-built models for "Tax Forms" and "Invoices" that work well with Czech documents.

Process: Extract key fields into a Typed C# Model (e.g., TaxDocumentContext).

The Agentic Loop:

Step A (Researcher): Retrieves the specific paragraphs related to the user's data (e.g., "§ 10 for RSU").

Step B (Calculator): A Native C# Plugin handles the math. Never let the LLM do the final math. Pass the numbers to a C# method that applies the formulas found in the law.

Step C (Verifier): Cross-references the result with the retrieved legal text to ensure no thresholds were missed.

2. Technical Stack for .NET
Orchestrator: Semantic Kernel (NuGet: Microsoft.SemanticKernel).

AI Model: GPT-4o (best for Czech nuances) or Claude 3.5 Sonnet.

Memory: Microsoft.SemanticKernel.Connectors.Qdrant or Azure.Search.Documents.

Exchange Rates: A simple HTTP client calling the ČNB API (XML/JSON) to convert foreign income to CZK.

3. Solving the XML Problem
Even if the user fills it manually, providing a pre-validated data schema is your "killer feature."

The .NET Implementation for XML:

Download the XSD Schema from the Financial Administration's EPO portal.

Use the xsd.exe tool (included in Visual Studio) to generate C# classes from that schema.

Your AI Agent populates these C# classes.

Serialize the classes into the final .xml file.

Sample Agent Setup in C#:

C#
// Example of a Native Plugin for the Kernel
public class TaxCalculationPlugin
{
    [KernelFunction, Description("Calculates tax for §10 other income")]
    public double CalculateSection10Tax(double income, double expenses)
    {
        // Solid C# logic, not flaky LLM math
        return (income - expenses) * 0.15;
    }
}

// Adding the plugin to the Kernel
var builder = Kernel.CreateBuilder();
builder.Plugins.AddFromType<TaxCalculationPlugin>();
var kernel = builder.Build();
4. Component Checklist
Hybrid Search: Essential for Czech law. Vector search finds "meaning," but Keyword search finds "Paragraph 38f" (which vectors often miss).

Unit Tests for Math: Build a suite of "Golden Cases" (standard tax scenarios) to verify the C# calculation logic daily.

Citation Engine: Ensure the Kernel is prompted to return metadata (source URL or PDF page number) with every answer.


For a .NET developer building a local tax assistant, Qdrant is the most highly recommended open-source vector database. It is written in Rust for high performance, has a first-class .NET SDK, and integrates natively with Semantic Kernel.Other strong local options include Chroma (for simplicity) and Milvus (for high-scale).1. Qdrant (Best for .NET/Semantic Kernel)Qdrant is frequently the "gold standard" for local .NET development because of its low resource footprint and mature C# client.How to run locally: The easiest way is via Docker.Bashdocker run -p 6333:6333 -p 6334:6334 \
    -v $(pwd)/qdrant_storage:/qdrant/storage \
    qdrant/qdrant
Why for you: It includes a built-in Web UI (at http://localhost:6333/dashboard) that lets you visually inspect your vectorized tax laws and documents.C# Integration:C#// Using Semantic Kernel Qdrant Connector
var memoryStore = new QdrantMemoryStore("http://localhost:6333", 1536);
2. Chroma (Best for Rapid Prototyping)Chroma is "AI-native" and designed to be incredibly simple. It is often the fastest way to get a RAG (Retrieval-Augmented Generation) pipeline running.How to run locally:Bashdocker run -p 8000:8000 chromadb/chroma
Why for you: If you want a "zero-config" experience, Chroma is excellent. It handles its own embedding persistence with very little boilerplate.Trade-off: While there is a community .NET client, it is not as "official" or robust as Qdrant’s.3. Milvus (Best for Scale)If your "tax folder" eventually grows into millions of documents (e.g., historical court rulings), Milvus is the heavyweight option.How to run locally: Use Milvus Lite (runs in a Python environment) or the standalone Docker image.Why for you: It is designed for massive scale and high-availability, but it is significantly more complex to configure than Qdrant.Comparison Table for Local UseFeatureQdrantChromaMilvusLanguageRust (Fast/Efficient)Python/C++Go/Python/C++Local DashboardYes (Built-in)No (Third-party only)Yes (Attu).NET SupportExcellent (Official)Good (Community)Good (Official)Setup EaseVery HighHighestMediumSummary RecommendationStick with Qdrant. It strikes the best balance for a .NET developer: it’s lightweight enough for a laptop, has the best Semantic Kernel support, and provides the visual dashboard you’ll need to debug why a specific tax paragraph was (or wasn't) retrieved.To see a practical demonstration of getting this environment running, this guide to setting up Qdrant locally will walk you through the Docker commands and the initial connection tests.

What if we built a tax advisor chat bot: user is presented with a web page with text input, user makes a prompt, AI agent responds. User can drop files and AI agent could ask user questions if any information is missing

To build a multi-turn Tax Advisor Chatbot in .NET that can handle file uploads, maintain context, and proactively ask for missing information, you should move from a linear pipeline to an Agentic Group Chat architecture using Semantic Kernel.

Here is the technical blueprint for that system.

1. Conceptual Architecture
The system functions as a "Consultation Room" where the User speaks to an Orchestrator Agent. This orchestrator has "experts" (sub-agents) it can call upon to analyze specific files or legal points.

Frontend: Blazor or React (SignalR for streaming AI responses).

Orchestrator: A Semantic Kernel AgentGroupChat.

State Management: An AgentThread to store the history of the conversation and the status of the tax return (what fields are "Full" vs. "Missing").

2. Multi-Agent Setup (.NET)
You will define specialized agents. In Semantic Kernel, these are ChatCompletionAgent instances.

Agent	Responsibility
Intake Agent	Analyzes uploaded files (RSUs, bank statements) and extracts data into a structured JSON.
Legal Auditor	Queries your local Qdrant Vector DB to find the relevant Czech tax laws for the user's specific case.
Validation Agent	Compare's the user's data against the legal requirements and identifies missing info (e.g., "You have dividends, but I don't see a W-8BEN confirmation").
The Interviewer	The public "face" of the bot. It translates the technical gaps into friendly questions for the user.
3. The "Missing Information" Logic
This is the hardest part to get right. You shouldn't rely on the LLM to "remember" what is missing. Instead, use a Structured State Machine:

Define a "Tax Context" Model: Create a C# class representing the DPFO fields (Income, Deductions, Personal Details).

State Verification Plugin: Create a Native C# Plugin that the agents call:

C#
public class TaxValidationPlugin {
    [KernelFunction]
    public string GetMissingFields(string currentDataJson) {
        var data = JsonSerializer.Deserialize<TaxReturn>(currentDataJson);
        var gaps = new List<string>();
        if (data.TotalIncome > 0 && data.HasForeignIncome && data.TaxPaidAbroad == 0) 
            gaps.Add("Confirmation of tax paid abroad");
        return gaps.Any() ? $"Missing: {string.Join(", ", gaps)}" : "Complete";
    }
}
Agent Instruction: Tell the Validation Agent: "After every file upload, call GetMissingFields. If the result is not 'Complete', tell the Interviewer Agent to ask the user for those specific items."

4. Pipeline for File Uploads
Since you are working with .NET, the flow for handling a "folder of files" looks like this:

Upload: User drops files into the Web UI.

OCR/Extraction: Send files to Azure AI Document Intelligence. It returns structured JSON.

Context Injection:

The extracted JSON is added to the Conversation Memory.

The files are vectorized and stored in Qdrant (local) so the AI can "search" them later if the user asks a specific question about a specific transaction.

Multi-Turn Questioning: If the AI finds a gap (e.g., "I see stock vesting but no sell-price"), it triggers the Interviewer Agent to pause the tax calculation and ask: "I've processed your RSUs, but I need the market value at the time of sale to calculate the gain. Do you have that document?"

5. Why this works for Tax
Proactive, not Reactive: By using a Validation Agent with a C# plugin, the bot doesn't just wait for the user; it actively drives the conversation toward a finished tax return.

Separation of Concerns: Your Intake Agent can be a cheaper/faster model (like GPT-4o-mini) for data extraction, while your Legal Auditor can be a high-reasoning model (like GPT-4o or O1) to interpret the law.

Traceability: Because you use Semantic Kernel, every question the bot asks can be linked back to a specific C# validation rule or a specific paragraph in your Qdrant-stored Czech law.

Next Steps for Implementation:

Set up a Web API project with Microsoft.SemanticKernel.Agents.Core.

Implement the TaxReturn C# class to track the "state" of the filing.

Use a Handoff Strategy (e.g., KernelFunctionSelectionStrategy) to let the AI decide when to switch from "Extracting data" to "Asking the user for more."

How do I create the knowledge database (RAG)?

To build a high-precision Knowledge Database (RAG) for Czech tax legislation in .NET, you need to move beyond "naive RAG" (just dumping text into a DB) and implement a Legal-Grade Ingestion Pipeline.

Here is the technical breakdown for your .NET implementation using Qdrant and Semantic Kernel.

1. The Data Ingestion Pipeline
Legal text is highly structured. If you split a paragraph (§) in half, the AI will lose the "exception" or "condition" at the end, leading to wrong tax advice.

A. Pre-processing & Extraction
Do not use basic PDF text readers. Use Azure AI Document Intelligence (Layout Model).

Why: It recognizes headers, bold text, and tables. In Czech tax law, tables (like tax brackets or depreciation groups) are critical.

Target Sources: * Zákon o daních z příjmů (Income Tax Act).

Pokyny řady D (Official instructions from the Financial Administration).

Sdělení MF (Ministry of Finance communications).

B. Semantic Chunking Strategy
Instead of splitting by "every 1000 characters," use Structural Chunking:

Level 1 (Document): Income Tax Act 2025.

Level 2 (Section): e.g., § 10 - Ostatní příjmy (Other Income).

Level 3 (Subsection): e.g., § 10 odst. 1 písm. b) (Income from sale of securities).

Context Injection: Append the Section title to every chunk.

Bad Chunk: "...if held for more than 3 years."

Good Chunk: "[Income Tax Act § 10 - Sale of Securities]: The income is exempt if held for more than 3 years."

2. The Vector Database (Local Qdrant Setup)
Since you want to run locally, use the Qdrant Docker image.

Docker Command:

Bash
docker run -p 6333:6333 qdrant/qdrant
C# Implementation (using Semantic Kernel):
Install NuGet: Microsoft.SemanticKernel.Connectors.Qdrant

C#
using Microsoft.SemanticKernel.Connectors.Qdrant;

// Initialize the local Qdrant memory store
var memoryStore = new QdrantMemoryStore("http://localhost:6333", 1536); // 1536 is for OpenAI embeddings

// Create a collection for specific tax years
await memoryStore.CreateCollectionAsync("czech-tax-2025");
3. The Retrieval Strategy (Hybrid Search)
Pure vector search (semantic) often fails on legal codes. If a user asks about "§ 38f", a vector search might return "tax credits" in general, but miss the exact paragraph.

Use Hybrid Search:

Vector Search: For conceptual questions ("How do I tax my US stocks?").

Keyword/Full-Text Search: For specific legal references ("§ 10", "W-8BEN", "RSU").

Note: Qdrant supports Filtering. You should store the tax year as metadata so you can filter for year == 2025 to avoid retrieving old, invalid laws.

4. Logic & Metadata Schema
When you "upsert" data into Qdrant, include a robust metadata object in the payload:

JSON
{
  "document_type": "Act",
  "paragraph_id": "10",
  "sub_paragraph": "1b",
  "effective_year": 2025,
  "source_url": "https://zakonyprolidi.cz/...",
  "text_content": "Příjmy z prodeje cenných papírů..."
}
Summary of the "Build" Workflow
Extract: Run your PDFs through Azure Document Intelligence to get Markdown/JSON.

Chunk: Write a C# script to split the Markdown by the "§" symbol.

Embed: Pass the text to an embedding model (e.g., text-embedding-3-small).

Store: Save the Vector + Metadata into Qdrant.

Query: Use Semantic Kernel's Memory plugin to search this collection during the chat.

Pro-Tip for .NET: Look into "Summary-Augmented Chunking." For every legal paragraph, have the AI generate a 1-sentence summary and store that with the chunk. This significantly improves retrieval accuracy for complex Czech legal phrasing.