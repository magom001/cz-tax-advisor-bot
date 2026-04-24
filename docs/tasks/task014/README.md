# Task 014 — Integration Tests: Golden-Case Tax Scenarios

## Objective

Build a comprehensive integration test suite with "golden case" tax scenarios that verify end-to-end correctness of the tax calculation pipeline.

## Work Items

1. Define golden-case scenarios in `tests/TaxAdvisorBot.Integration.Tests`:
   - **Scenario A**: §6 employment income only — basic salary with social/health insurance.
   - **Scenario B**: §10 RSU income — US stock vesting, conversion via ČNB rate, tax calculation.
   - **Scenario C**: §8 capital gains — stock sale with acquisition cost deduction.
   - **Scenario D**: Combined §6 + §10 — employment + foreign dividends, tax credit method (§38f).
   - **Scenario E**: Missing information — verify the system identifies gaps and asks for them.
2. Each scenario provides:
   - Input: structured `TaxReturn` with known values.
   - Expected output: exact tax amount, applicable sections, required citations.
3. Test the full pipeline: input → plugin calculations → validation → output.
4. Test exchange rate conversion with known historical rates.
5. Mark tests that require Docker/Azure with `[Trait("Category", "Integration")]`.

## Expected Results

- All golden-case scenarios produce expected tax amounts (to the CZK).
- Missing-field detection correctly identifies gaps in each scenario.
- Citations reference the correct § for each calculation.
- Tests are runnable in CI with Docker (Qdrant) and Azure AI credentials.
- Any regression in tax calculation logic is immediately caught.
