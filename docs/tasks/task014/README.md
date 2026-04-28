# Task 014 — Integration Tests: Golden-Case Tax Scenarios

## Objective

Build comprehensive integration tests that verify end-to-end correctness of the tax calculation pipeline. These test the plugins directly — not the agents (agent behavior is non-deterministic).

## Work Items

1. Define golden-case scenarios in `tests/TaxAdvisorBot.Integration.Tests`:
   - **Scenario A**: §6 employment income only — salary + insurance, basic taxpayer credit.
   - **Scenario B**: §10 RSU income — US stock vesting, ČNB rate conversion, 15% tax.
   - **Scenario C**: Share sale with 3-year exemption — verify `IsExemptFromTax`.
   - **Scenario D**: Combined §6 + §10 + §15 deductions + §35ba credits — full pipeline.
   - **Scenario E**: Solidarity surcharge — income above threshold, 23% rate on excess.
   - **Scenario F**: Child tax bonus — credits exceed tax, negative result (refund).
   - **Scenario G**: Missing information — `TaxValidationPlugin.GetMissingFields` identifies gaps.
2. Each scenario: known input `TaxReturn` → call plugin methods → assert exact CZK amounts.
3. Test exchange rate conversion with known historical rates (mocked ČNB response).
4. Test ESPP discount calculation (10% discount, §6 income).

## Expected Results

- All scenarios produce expected tax amounts (to the CZK).
- Regressions in plugin math are caught immediately.
- No Azure/Docker dependencies — pure unit tests on plugins.
