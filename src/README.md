# googlesheets-service

Simple service to read and write to Google Sheets using `Google.Apis.Sheets.v4`, with optional **Polly v8 resilience** (retry, circuit breaker, timeout, bulkhead).

## Setup

1. Create a [Google Cloud project](https://console.cloud.google.com/).
2. Enable the Google Sheets API for the project.
3. Create a service account.
4. Create a key for the service account and download it as JSON.
5. Set the environment variable `GOOGLE_APPLICATION_CREDENTIALS` to the path of the JSON file.
6. Copy the service account email → open the Google Sheet → share it with that email.

## Dependency Injection (recommended)

Register the service with `AddGoogleSheetsService`. Resilience is **opt-in per policy** — all policies default to disabled.

```csharp
// Program.cs
builder.Services.AddGoogleSheetsService(builder.Configuration);
```

Inject and use `IGoogleSheetsService` in your classes:

```csharp
public class MyService(IGoogleSheetsService sheets) { ... }
```

### Resilience configuration

Configure any combination of policies in `appsettings.json` under `GoogleSheetsService:Resilience`. Policies not listed (or with `Enabled: false`) are skipped.

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 3,
        "InitialDelayMs": 100,
        "MaxDelayMs": 5000,
        "BackoffMultiplier": 2.0
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 5,
        "FailureWindowSeconds": 60,
        "SuccessThresholdInHalfOpenState": 2,
        "CircuitTimeoutSeconds": 30
      },
      "Timeout": {
        "Enabled": true,
        "TimeoutSeconds": 30
      },
      "Bulkhead": {
        "Enabled": true,
        "MaxParallelization": 20,
        "MaxQueueingActions": 50
      }
    }
  }
}
```

| Policy | Default | Description |
|---|---|---|
| `Retry` | disabled | Exponential backoff with jitter on transient failures |
| `CircuitBreaker` | disabled | Opens circuit after failure threshold to prevent cascading failures |
| `Timeout` | disabled | Cancels operations that exceed the configured duration |
| `Bulkhead` | disabled | Limits concurrent operations via a semaphore |

When any policy is enabled, `IGoogleSheetsService` is automatically decorated with `GoogleSheetsServiceWithResilience`.

### Resilience telemetry

Monitor operation metrics by injecting `IResilienceTelemetry`:

```csharp
app.MapGet("/health/resilience", (IResilienceTelemetry telemetry) =>
{
    var m = telemetry.GetMetrics();
    return Results.Ok(new
    {
        m.SuccessCount,
        m.RetryCount,
        m.CircuitBreakerTrippedCount,
        m.TimeoutCount,
        m.BulkheadRejectionCount,
        SuccessRate = m.SuccessRatePercentage
    });
});
```

## Usage examples

### Read

```csharp
var spreadsheetId = "<your sheet ID>";
var sheetName = "Sheet1";
var range = "A2:B2";

var data = await sheetsService.ReadSheetAsync(spreadsheetId, sheetName, range);
```

### Batch read (multiple ranges)

```csharp
var results = await sheetsService.BatchGetValuesAsync(
    spreadsheetId,
    new[] { "Sheet1!A1:Z", "Sheet2!A1:C" }
);
```

### Write

```csharp
var values = new List<IList<object>>
{
    new List<object> { "Name", "Age" },
    new List<object> { "Alice", 25 },
    new List<object> { "Bob", 30 }
};

await sheetsService.WriteSheetAsync(spreadsheetId, sheetName, "A1:B3", values);
```

### Append to the last empty row

```csharp
var people = new List<Person>
{
    new Person("Alice", 25, "123 Main St"),
    new Person("Bob", 30, "456 Oak Ave")
};

// Convert to IList<IList<object>> via reflection helper
var values = people.ToGoogleSheetsValues();

await sheetsService.AppendToEndAsync(spreadsheetId, sheetName, values);

public record Person(string Name, int Age, string Address);
```
