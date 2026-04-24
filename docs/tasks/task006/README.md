# Task 006 — ČNB Exchange Rate Service

## Objective

Implement the exchange rate service that fetches daily rates from the Czech National Bank (ČNB) API and converts foreign income to CZK.

## Work Items

1. Implement `CnbExchangeRateService : IExchangeRateService`:
   - Fetch daily exchange rates from ČNB API (XML/TXT endpoint).
   - Parse the response into a typed model.
   - Cache rates for the day (avoid repeated HTTP calls).
   - Support historical date lookups (for tax year rates).
2. Create `ExchangeRatePlugin` as a Semantic Kernel `[KernelFunction]`:
   - `ConvertToCzk(amount, currencyCode, date)` — uses `IExchangeRateService`.
3. Register in DI with `HttpClient` via `IHttpClientFactory`.
4. Write unit tests:
   - Parse known ČNB response format.
   - Verify conversion math (e.g., 1000 USD at known rate).
   - Verify caching behavior (second call doesn't hit HTTP).
5. Write integration test against live ČNB API (marked with `[Trait("Category", "Integration")]`).

## Expected Results

- `CnbExchangeRateService` correctly parses ČNB exchange rate data.
- Currency conversion produces accurate CZK amounts.
- Caching prevents redundant API calls.
- Unit tests pass without network access (mocked HTTP).
- Integration test confirms live API compatibility.
