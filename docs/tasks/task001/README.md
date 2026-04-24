# Task 001 — Solution Scaffolding & Aspire Setup

## Objective

Create the .NET solution structure with all projects, configure .NET Aspire orchestration, and verify everything builds.

## Work Items

1. Create `TaxAdvisorBot.sln` at the repo root.
2. Create the Aspire projects:
   - `src/TaxAdvisorBot.AppHost` (Aspire orchestrator)
   - `src/TaxAdvisorBot.ServiceDefaults` (shared telemetry, health checks, resilience)
3. Create the core class libraries:
   - `src/core/TaxAdvisorBot.Domain`
   - `src/core/TaxAdvisorBot.Application` (references Domain)
   - `src/core/TaxAdvisorBot.Infrastructure` (references Application + Domain)
4. Create platform projects:
   - `src/platforms/TaxAdvisorBot.Web` (ASP.NET Core, references Application)
   - `src/platforms/TaxAdvisorBot.Cli` (Console, references Application)
5. Create test projects:
   - `tests/TaxAdvisorBot.Domain.Tests`
   - `tests/TaxAdvisorBot.Application.Tests`
   - `tests/TaxAdvisorBot.Infrastructure.Tests`
6. Wire up project references according to the dependency flow (Domain → Application → Infrastructure; Platforms → Application only).
7. Register all platform projects in AppHost.
8. Add `ServiceDefaults` reference to all runnable projects.
9. Verify `dotnet build` succeeds.
10. Verify `dotnet test` runs (even with zero tests).

## Expected Results

- Solution builds with zero errors and zero warnings.
- `dotnet run --project src/TaxAdvisorBot.AppHost` launches the Aspire dashboard.
- Project references enforce the dependency flow — no circular or illegal references.
- Test projects are discovered by `dotnet test`.

## Notes

- Do **not** add NuGet packages beyond what Aspire templates provide. Packages are added in later tasks.
- Telegram platform project is deferred to a later task.
