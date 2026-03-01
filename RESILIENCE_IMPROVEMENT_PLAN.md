# Resilience Improvement Plan for Googlesheets-Service

## 📌 Background

A service log indicates that resilience configuration in `libs/googlesheets-service` is ineffective.
The library continues to handle retries internally via `try/catch` loops, bypassing the configured Polly pipeline. The log messages such as:

```
Too many requests, waiting for 1 minute
```

originate from these manual loops. They also obscure what the resilience policies actually do, and other exceptions are not clearly attributed to any policy.

### Current retry behaviour

- Only retries when a `GoogleApiException` with `HttpStatusCode.TooManyRequests` (429) occurs.
- Waits one minute between attempts using `ITimeProvider.Delay`.
- Internal loops are present in several methods (`ReadSheetAsync`, `ReadSheetInChunksAsync`, `BatchGetValuesAsync`) and their chunked variants.

The requirement is to **preserve this exact retry condition** while moving the mechanism to the Polly-based pipeline.

---

## 🎯 Objectives

1. **Remove manual retry loops** from service implementations.
2. **Ensure resilience policies handle the same failure cases** currently retried (429 only).
3. **Expose and respect configuration** (`Retry`, `CircuitBreaker`, `Timeout`, `Bulkhead`) via `ResilienceOptions`.
4. **Enhance telemetry/logging** to report policy events.
5. **Expand testing** so behaviour is verified end‑to‑end using both mock and real wrappers.
6. **Document the changes** for consumers and update sample configuration.

---

## 🗂 Detailed Plan

### 1. Refactor service logic

- **Strip out all `while(true)`/`catch` loops** that retry for too-many-requests or bad request. Those loops should either:
  - be replaced by single calls wrapped by the pipeline (for methods that don't accumulate results); or
  - emit partial results for 400 errors but let 429 bubble out.

- Example refactor for `ReadSheetAsync`:

  ```csharp
  public async Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range)
  {
      var response = await _sheetsServiceWrapper.GetValuesAsync(spreadsheetId, $"{sheetName}!{range}");
      return response?.Values;
  }
  ```

- For chunked methods, either call the pipeline per-chunk or allow exceptions to propagate out of the chunking loop so the pipeline retries the whole operation.

### 2. Update `ResiliencePolicyFactory`

- **Adjust retry policy** to limit retries to the same condition as current behaviour:
  - retry only when exception is `GoogleApiException` with `HttpStatusCode.TooManyRequests`.
  - optionally include transient network exceptions (match current loops which already catch only 429; preserve that constraint).

- Use configured backoff multiplier and respect `RetryOptions` fields.

- Pass telemetry into callbacks (`OnRetry`, `OnTimeout`, etc.) to increment counters.

- Add an optional `ShouldRetry` predicate reflecting the above.

- Example snippet:
  ```csharp
  ShouldRetry = context =>
      context.Exception is GoogleApiException ga &&
      ga.HttpStatusCode == HttpStatusCode.TooManyRequests;
  ```

### 3. Telemetry & logging

- In each strategy's callbacks, call `IResilienceTelemetry`:
  - OnRetry → `IncrementRetry()`
  - OnTimeout → `IncrementTimeout()`
  - OnOpened → `IncrementCircuitBreakerTripped()`
  - Bulkhead rejection → `IncrementBulkheadRejection()`
  - On successful execution → increment success counter somewhere (pipeline wrapper?).

- Ensure `TelemetryOptions.LogLevel` controls logger verbosity.

### 4. Tests

- **Unit tests for `GoogleSheetsService`** verifying that with resilience enabled, a simulated 429 causes the pipeline to retry (increase attempt count) and that `ITimeProvider` is _not_ used for the manual wait.

- **End‑to‑end tests** updated to use a fake `ISheetsServiceWrapper` that throws `GoogleApiException` with 429, verifying the pipeline triggers only under that condition.

- **Configuration tests** to cover the new `ShouldRetry` predicate and `BackoffMultiplier` property.

- Add tests confirming telemetry counters increment for retry/timeouts, and that circuit-breaker/bulkhead behave when enabled.

### 5. Documentation

- Create or update `README.md` (or add new `RESILIENCE.md`) with:
  - Explanation of each policy and how to configure it.
  - Example `appsettings.json` snippet.
  - Clarification that only 429
