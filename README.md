# TaxAdvisorBot

AI-powered personal tax advisor for Czech income tax (DPFO). Helps employed persons with stock compensation (RSU, ESPP, share sales) prepare their yearly income tax return, including an uploadable XML file and PDF form.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Redis, Qdrant)
- Azure AI Foundry account with deployed models

## Quick Start

```bash
# 1. Clone and build
dotnet build TaxAdvisorBot.slnx

# 2. Configure secrets (see below)

# 3. Run with Aspire
dotnet run --project src/TaxAdvisorBot.AppHost
```

The Aspire dashboard will open in your browser showing all services, Redis, and telemetry.

## Azure AI Setup

### 1. Deploy Models in Azure AI Foundry

Go to [Azure AI Foundry](https://ai.azure.com) and deploy the following models:

| Deployment Name | Model | Purpose |
|---|---|---|
| `gpt-4.1` | GPT-4.1 | Primary chat — legal analysis, complex tax questions, citation generation |
| `gpt-4.1-mini` | GPT-4.1 Mini | Fast/cheap — document data extraction, classification, simple Q&A |
| `o4-mini` | o4-mini | Reasoning — multi-step tax planning, verification, edge case analysis |
| `text-embedding-ada-002` | text-embedding-ada-002 | Embeddings — vectorization of Czech tax law for RAG search |
| `gpt-5.1` | GPT-5.1 | (Optional) Premium model for the most complex legal reasoning |

> **Note:** Deployment names can be customized. Use whatever names you set in Azure AI Foundry — just match them in the configuration below.

### 2. Get Your Endpoint and API Key

From your Azure AI Foundry resource:
- **Endpoint**: `https://<your-resource>.openai.azure.com/`
- **API Key**: Found under "Keys and Endpoint" in the Azure portal

### 3. Configure Secrets

Use `dotnet user-secrets` for each platform project. **Never commit secrets to source control.**

#### Web Platform

```bash
cd src/platforms/TaxAdvisorBot.Web

dotnet user-secrets init
dotnet user-secrets set "AzureAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "AzureAI:ChatDeploymentName" "gpt-4.1"
dotnet user-secrets set "AzureAI:FastChatDeploymentName" "gpt-4.1-mini"
dotnet user-secrets set "AzureAI:ReasoningDeploymentName" "o4-mini"
dotnet user-secrets set "AzureAI:EmbeddingDeploymentName" "text-embedding-ada-002"
```

#### CLI Platform

```bash
cd src/platforms/TaxAdvisorBot.Cli

dotnet user-secrets init
dotnet user-secrets set "AzureAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "AzureAI:ChatDeploymentName" "gpt-4.1"
dotnet user-secrets set "AzureAI:FastChatDeploymentName" "gpt-4.1-mini"
dotnet user-secrets set "AzureAI:ReasoningDeploymentName" "o4-mini"
dotnet user-secrets set "AzureAI:EmbeddingDeploymentName" "text-embedding-ada-002"
```

### 4. Verify Configuration

The app validates all configuration on startup. If a required field is missing, you'll get a clear error message:

```
Microsoft.Extensions.Options.OptionsValidationException:
  DataAnnotation validation failed for 'AzureAIOptions' members: 'Endpoint', 'ApiKey' ...
```

## Model Usage Guide

| Agent / Task | Model | Why |
|---|---|---|
| **Legal Auditor** — RAG search + Czech law interpretation | `gpt-4.1` | Best balance of accuracy and Czech language understanding |
| **Document Extraction** — parse uploaded PDFs/images | `gpt-4.1-mini` | Fast, cheap, good enough for structured extraction |
| **Tax Verifier** — cross-reference calculations against law | `o4-mini` | Reasoning model excels at multi-step logical verification |
| **Interviewer** — conversational Q&A with the user | `gpt-4.1-mini` | Low latency for interactive chat |
| **RAG Embeddings** — vectorize Czech tax legislation | `text-embedding-ada-002` | Standard embedding model for Qdrant vector search |
| **Complex Planning** — multi-year tax optimization | `gpt-5.1` | (Optional) Premium reasoning for edge cases |

## Configuration Reference

All options are in the `AzureAI` section. Matching C# class: `AzureAIOptions`.

| Key | Required | Description |
|---|---|---|
| `AzureAI:Endpoint` | Yes | Azure AI Foundry endpoint URL |
| `AzureAI:ApiKey` | Yes | API key |
| `AzureAI:ChatDeploymentName` | Yes | Primary chat model (e.g. `gpt-4.1`) |
| `AzureAI:FastChatDeploymentName` | Yes | Fast/cheap model (e.g. `gpt-4.1-mini`) |
| `AzureAI:ReasoningDeploymentName` | No | Reasoning model (e.g. `o4-mini`) |
| `AzureAI:EmbeddingDeploymentName` | Yes | Embedding model (e.g. `text-embedding-ada-002`) |
| `Qdrant:Endpoint` | Yes | Qdrant vector DB URL (default: managed by Aspire) |
| `Qdrant:CollectionName` | Yes | Collection name (default: `czech-tax`) |
| `Qdrant:VectorSize` | Yes | Embedding dimensions (default: `1536`) |

## Running Tests

```bash
dotnet test TaxAdvisorBot.slnx
```

## Project Structure

See [docs/Architecture.md](docs/Architecture.md) for the full architecture and dependency flow.
