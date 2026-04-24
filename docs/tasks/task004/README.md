# Task 004 — Infrastructure: Semantic Kernel Setup & Tax Calculation Plugins

## Objective

Set up the Semantic Kernel orchestrator in Infrastructure, implement the first deterministic C# tax calculation plugin, and register it with the kernel.

## Work Items

1. Add NuGet packages: `Microsoft.SemanticKernel`, Azure AI connectors.
2. Create `SemanticKernelOrchestrator` — builds and configures the Kernel instance, registers plugins, connects to Azure AI Foundry endpoint.
3. Implement `TaxCalculationPlugin` with `[KernelFunction]` methods:
   - `CalculateSection10Tax(income, expenses)` — §10 other income (basic rate).
   - `CalculateSection6Tax(grossSalary, socialInsurance, healthInsurance)` — §6 employment.
4. Implement `TaxValidationPlugin`:
   - `GetMissingFields(TaxReturn)` — deterministic C# check returning list of missing/invalid fields.
5. Create `IServiceCollection` extension `AddInfrastructureServices()` that registers the kernel and plugins.
6. Write unit tests for every calculation method with known inputs/outputs.
7. Write unit tests for `GetMissingFields` covering complete and incomplete `TaxReturn` instances.

## Expected Results

- `TaxCalculationPlugin` methods produce correct results for known tax scenarios.
- `TaxValidationPlugin.GetMissingFields` correctly identifies missing data.
- All plugins are registered in DI and resolvable.
- Unit tests pass in `TaxAdvisorBot.Infrastructure.Tests`.
- LLM is never involved in arithmetic — all math is deterministic C#.
