# Implementation Document: Polly Resilience Patterns in GoogleSheetsService

## Executive Summary

This document provides a detailed implementation plan for integrating **Polly** resilience patterns (retry, circuit breaker, timeout, bulkhead) into the `GoogleSheetsService` library with full opt-in/opt-out capability at the client level.

**Key Design Principle**: Clients of the library can enable or disable resilience mechanisms via configuration, maintaining backward compatibility.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Application                        │
│        (Configures via appsettings.json / code)              │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│         GoogleSheetsServiceResilienceBuilder                 │
│    (Conditional Polly policy registration based on config)   │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────┴────────────┐
        ▼                         ▼
  ┌──────────────┐      ┌──────────────────┐
  │ With Polly   │      │ Without Polly     │
  │ (Enabled)    │      │ (Disabled)        │
  └────────┬─────┘      └────────┬──────────┘
           │                     │
           ▼                     ▼
    ┌─────────────┐      ┌──────────────────┐
    │GoogleSheets │      │GoogleSheetsService
    │ServicePolly │      │(Original behavior)
    │Wrapper      │      └──────────────────┘
    └──────┬──────┘
           │
           ▼
  ┌──────────────────────────────┐
  │  Polly Resilience Policies   │
  │  - RetryPolicy               │
  │  - CircuitBreakerPolicy      │
  │  - TimeoutPolicy             │
  │  - BulkheadPolicy            │
  │  - PolicyWrap (combined)     │
  └──────────────────────────────┘
```

---

## 1. Configuration Schema

### appsettings.json Configuration

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
      },
      "Telemetry": {
        "Enabled": true,
        "LogLevel": "Information"
      }
    }
  }
}
```

**Key Points:**

- No top-level `Enabled` flag - resilience is automatically enabled if **any** policy is enabled
- Each policy has its own `Enabled` flag (default: `false` unless explicitly set)
- Clients can enable/disable policies independently (e.g., Retry ✅ but Timeout ❌)
- If a policy section is omitted, that policy won't execute

### Options Classes

```csharp
// ResilienceOptions.cs
namespace GoogleSheetsService.Resilience
{
    public class ResilienceOptions
    {
        public const string SectionKey = "GoogleSheetsService:Resilience";

        public RetryOptions Retry { get; set; } = new();
        public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
        public TimeoutOptions Timeout { get; set; } = new();
        public BulkheadOptions Bulkhead { get; set; } = new();
        public TelemetryOptions Telemetry { get; set; } = new();

        /// <summary>
        /// Determines if resilience is enabled based on whether any policy is enabled.
        /// </summary>
        public bool IsEnabled => Retry.Enabled || CircuitBreaker.Enabled || Timeout.Enabled || Bulkhead.Enabled;
    }

    public class RetryOptions
    {
        /// <summary>
        /// Enable retry policy with exponential backoff and jitter.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;
        public int MaxRetries { get; set; } = 3;
        public int InitialDelayMs { get; set; } = 100;
        public int MaxDelayMs { get; set; } = 5000;
        public double BackoffMultiplier { get; set; } = 2.0;
    }

    public class CircuitBreakerOptions
    {
        /// <summary>
        /// Enable circuit breaker to prevent cascading failures.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;
        public int FailureThreshold { get; set; } = 5;
        public int FailureWindowSeconds { get; set; } = 60;
        public int SuccessThresholdInHalfOpenState { get; set; } = 2;
        public int CircuitTimeoutSeconds { get; set; } = 30;
    }

    public class TimeoutOptions
    {
        /// <summary>
        /// Enable timeout policy to prevent hanging requests.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class BulkheadOptions
    {
        /// <summary>
        /// Enable bulkhead policy to limit concurrent operations.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;
        public int MaxParallelization { get; set; } = 20;
        public int MaxQueueingActions { get; set; } = 50;
    }

    public class TelemetryOptions
    {
        public bool Enabled { get; set; } = true;
        public string LogLevel { get; set; } = "Information";
    }
}
```

---

## 2. Core Components

### 2.1 Resilience Policy Factory

```csharp
// ResiliencePolicyFactory.cs
namespace GoogleSheetsService.Resilience
{
    using Polly;
    using Polly.CircuitBreaker;
    using Polly.Bulkhead;
    using Polly.Timeout;
    using Microsoft.Extensions.Logging;

    public interface IResiliencePolicyFactory
    {
        IAsyncPolicy<T> CreatePolicy<T>(string policyName);
        IAsyncPolicy CreatePolicy(string policyName);
    }

    public class ResiliencePolicyFactory : IResiliencePolicyFactory
    {
        private readonly ResilienceOptions _options;
        private readonly ILogger _logger;
        private readonly Dictionary<string, IAsyncPolicy> _policies = new();
        private readonly Dictionary<string, IAsyncPolicy<object>> _policiesGeneric = new();

        public ResiliencePolicyFactory(ResilienceOptions options, ILogger logger)
        {
            _options = options;
            _logger = logger;

            if (options.IsEnabled)
            {
                _logger.LogInformation("Initializing Polly resilience policies");
                InitializePolicies();
            }
        }

        private void InitializePolicies()
        {
            // Create individual policies
            var retryPolicy = CreateRetryPolicy();
            var circuitBreakerPolicy = CreateCircuitBreakerPolicy();
            var timeoutPolicy = CreateTimeoutPolicy();
            var bulkheadPolicy = CreateBulkheadPolicy();

            // Combine policies using PolicyWrap
            var wrappedPolicy = Policy.WrapAsync(
                retryPolicy,
                circuitBreakerPolicy,
                timeoutPolicy,
                bulkheadPolicy
            );

            _policies["GoogleSheets"] = wrappedPolicy;
            _logger.LogInformation("Resilience policies initialized successfully");
        }

        private IAsyncPolicy CreateRetryPolicy()
        {
            if (!_options.Retry.Enabled)
                return Policy.NoOpAsync();

            return Policy.Handle<Exception>()
                .Or<HttpRequestException>()
                .OrInner<IOException>()
                .WaitAndRetryAsync(
                    retryCount: _options.Retry.MaxRetries,
                    sleepDurationProvider: attempt =>
                    {
                        // Exponential backoff with jitter
                        var delay = _options.Retry.InitialDelayMs *
                                   Math.Pow(_options.Retry.BackoffMultiplier, attempt);

                        var jitter = Random.Shared.Next(-50, 50);
                        var actualDelay = Math.Min((long)delay + jitter, _options.Retry.MaxDelayMs);

                        return TimeSpan.FromMilliseconds(actualDelay);
                    },
                    onRetry: (outcome, duration, retryNumber, context) =>
                    {
                        _logger.LogWarning(
                            "Retry attempt {RetryNumber} after {Duration}ms. " +
                            "Error: {ErrorMessage}",
                            retryNumber,
                            duration.TotalMilliseconds,
                            outcome.Exception?.Message
                        );
                    }
                );
        }

        private IAsyncPolicy CreateCircuitBreakerPolicy()
        {
            if (!_options.CircuitBreaker.Enabled)
                return Policy.NoOpAsync();

            return Policy.Handle<Exception>()
                .OrInner<IOException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: _options.CircuitBreaker.FailureThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(_options.CircuitBreaker.CircuitTimeoutSeconds),
                    onBreak: (outcome, duration) =>
                    {
                        _logger.LogError(
                            "Circuit breaker opened. Duration: {Duration}s. " +
                            "Error: {ErrorMessage}",
                            duration.TotalSeconds,
                            outcome.Exception?.Message
                        );
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset to closed state");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker entered half-open state");
                    }
                );
        }

        private IAsyncPolicy CreateTimeoutPolicy()
        {
            if (!_options.Timeout.Enabled)
                return Policy.NoOpAsync();

            return Policy.TimeoutAsync(
                TimeSpan.FromSeconds(_options.Timeout.TimeoutSeconds),
                TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, duration, _, _) =>
                {
                    _logger.LogWarning(
                        "Operation timed out after {Duration}s",
                        duration.TotalSeconds
                    );
                    return Task.CompletedTask;
                }
            );
        }

        private IAsyncPolicy CreateBulkheadPolicy()
        {
            if (!_options.Bulkhead.Enabled)
                return Policy.NoOpAsync();

            return Policy.BulkheadAsync(
                parallelization: _options.Bulkhead.MaxParallelization,
                maxParallerizationBufferSize: _options.Bulkhead.MaxQueueingActions,
                onBulkheadRejectedAsync: context =>
                {
                    _logger.LogWarning(
                        "Bulkhead policy rejected request. " +
                        "Max parallelization reached: {MaxParallelization}",
                        _options.Bulkhead.MaxParallelization
                    );
                    return Task.CompletedTask;
                }
            );
        }

        public IAsyncPolicy<T> CreatePolicy<T>(string policyName)
        {
            if (!_options.IsEnabled)
                return Policy.NoOpAsync<T>();

            return _policies.TryGetValue(policyName, out var policy)
                ? (IAsyncPolicy<T>)(object)policy
                : Policy.NoOpAsync<T>();
        }

        public IAsyncPolicy CreatePolicy(string policyName)
        {
            if (!_options.IsEnabled)
                return Policy.NoOpAsync();

            return _policies.TryGetValue(policyName, out var policy)
                ? policy
                : Policy.NoOpAsync();
        }
    }
}
```

### 2.2 Polly-Wrapped Google Sheets Service

```csharp
// GoogleSheetsServiceWithResilience.cs
namespace GoogleSheetsService.Resilience
{
    using Polly;
    using Microsoft.Extensions.Logging;

    public class GoogleSheetsServiceWithResilience : IGoogleSheetsService
    {
        private readonly IGoogleSheetsService _innerService;
        private readonly IAsyncPolicy _policy;
        private readonly ILogger _logger;

        public GoogleSheetsServiceWithResilience(
            IGoogleSheetsService innerService,
            IAsyncPolicy policy,
            ILogger logger)
        {
            _innerService = innerService;
            _policy = policy;
            _logger = logger;
        }

        public async Task AddSheetAsync(string spreadSheetId, string sheetName)
        {
            await _policy.ExecuteAsync(
                () => _innerService.AddSheetAsync(spreadSheetId, sheetName)
            );
        }

        public async Task<IList<IList<object>>?> ReadSheetAsync(
            string spreadsheetId,
            string sheetName,
            string range)
        {
            return await _policy.ExecuteAsync(
                () => _innerService.ReadSheetAsync(spreadsheetId, sheetName, range)
            );
        }

        public async Task<IList<IList<object>>?> ReadSheetInChunksAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            int chunkSize = 1000)
        {
            return await _policy.ExecuteAsync(
                () => _innerService.ReadSheetInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    chunkSize
                )
            );
        }

        public async Task<Dictionary<string, IList<IList<object>>>?> BatchGetValuesAsync(
            string spreadsheetId,
            string[] ranges)
        {
            return await _policy.ExecuteAsync(
                () => _innerService.BatchGetValuesAsync(spreadsheetId, ranges)
            );
        }

        public async Task WriteSheetAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values)
        {
            await _policy.ExecuteAsync(
                () => _innerService.WriteSheetAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values
                )
            );
        }

        public async Task AppendToEndAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values,
            string columnsRange = "A:Z")
        {
            await _policy.ExecuteAsync(
                () => _innerService.AppendToEndAsync(
                    spreadsheetId,
                    sheetName,
                    values,
                    columnsRange
                )
            );
        }

        public async Task WriteFromSecondRowAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values)
        {
            await _policy.ExecuteAsync(
                () => _innerService.WriteFromSecondRowAsync(
                    spreadsheetId,
                    sheetName,
                    values
                )
            );
        }

        public async Task DeleteRowsAsync(
            string spreadSheetId,
            string spreadSheetName,
            int fromRow)
        {
            await _policy.ExecuteAsync(
                () => _innerService.DeleteRowsAsync(
                    spreadSheetId,
                    spreadSheetName,
                    fromRow
                )
            );
        }

        public async Task ReplaceFromSecondRowAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values)
        {
            await _policy.ExecuteAsync(
                () => _innerService.ReplaceFromSecondRowAsync(
                    spreadsheetId,
                    sheetName,
                    values
                )
            );
        }

        public async Task ReplaceFromSecondRowInChunksAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values,
            int chunkSize)
        {
            await _policy.ExecuteAsync(
                () => _innerService.ReplaceFromSecondRowInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    values,
                    chunkSize
                )
            );
        }

        public async Task ReplaceFromRangeAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values)
        {
            await _policy.ExecuteAsync(
                () => _innerService.ReplaceFromRangeAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values
                )
            );
        }

        public async Task ClearValuesByRangeAsync(
            string spreadsheetId,
            string sheetName,
            string range)
        {
            await _policy.ExecuteAsync(
                () => _innerService.ClearValuesByRangeAsync(
                    spreadsheetId,
                    sheetName,
                    range
                )
            );
        }

        public async Task WriteSheetInChunksAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values,
            int chunkSize)
        {
            await _policy.ExecuteAsync(
                () => _innerService.WriteSheetInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values,
                    chunkSize
                )
            );
        }

        public async Task ReplaceFromRangeInChunksAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values,
            int chunkSize)
        {
            await _policy.ExecuteAsync(
                () => _innerService.ReplaceFromRangeInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values,
                    chunkSize
                )
            );
        }
    }
}
```

### 2.3 Resilience Telemetry

```csharp
// ResilienceTelemetry.cs
namespace GoogleSheetsService.Resilience
{
    using Polly;

    public interface IResilienceTelemetry
    {
        ResilienceMetrics GetMetrics();
        void Reset();
    }

    public class ResilienceTelemetry : IResilienceTelemetry
    {
        private int _successCount;
        private int _retryCount;
        private int _circuitBreakerTrippedCount;
        private int _timeoutCount;
        private int _bulkheadRejectionCount;

        public void IncrementSuccess() => Interlocked.Increment(ref _successCount);
        public void IncrementRetry() => Interlocked.Increment(ref _retryCount);
        public void IncrementCircuitBreakerTripped() => Interlocked.Increment(ref _circuitBreakerTrippedCount);
        public void IncrementTimeout() => Interlocked.Increment(ref _timeoutCount);
        public void IncrementBulkheadRejection() => Interlocked.Increment(ref _bulkheadRejectionCount);

        public ResilienceMetrics GetMetrics() => new()
        {
            SuccessCount = _successCount,
            RetryCount = _retryCount,
            CircuitBreakerTrippedCount = _circuitBreakerTrippedCount,
            TimeoutCount = _timeoutCount,
            BulkheadRejectionCount = _bulkheadRejectionCount
        };

        public void Reset()
        {
            _successCount = 0;
            _retryCount = 0;
            _circuitBreakerTrippedCount = 0;
            _timeoutCount = 0;
            _bulkheadRejectionCount = 0;
        }
    }

    public class ResilienceMetrics
    {
        public int SuccessCount { get; init; }
        public int RetryCount { get; init; }
        public int CircuitBreakerTrippedCount { get; init; }
        public int TimeoutCount { get; init; }
        public int BulkheadRejectionCount { get; init; }

        public int TotalAttempts => SuccessCount + RetryCount + CircuitBreakerTrippedCount + TimeoutCount;
        public double SuccessRate => TotalAttempts > 0 ? (double)SuccessCount / TotalAttempts : 0;
    }
}
```

---

## 3. Dependency Injection Extensions

### Updated Extensions.cs

```csharp
// Extensions.cs (Updated)
namespace GoogleSheetsService
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using GoogleSheetsService.Resilience;

    public static class Extensions
    {
        /// <summary>
        /// Adds GoogleSheetsService to the dependency injection container with optional Polly resilience.
        /// </summary>
        public static IServiceCollection AddGoogleSheetsService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register the core service
            services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();

            // Configure resilience options (uses class defaults if not in config)
            var resilienceSection = configuration.GetSection(ResilienceOptions.SectionKey);
            services.Configure<ResilienceOptions>(resilienceSection);

            // Register resilience components factory
            services.AddSingleton<IResiliencePolicyFactory>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
                var logger = provider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GoogleSheetsService.Resilience");
                return new ResiliencePolicyFactory(options, logger);
            });

            // Register telemetry
            services.AddSingleton<IResilienceTelemetry, ResilienceTelemetry>();

            // Conditionally decorate the service based on configuration
            // If any policy is enabled, wrap with resilience
            services.Decorate<IGoogleSheetsService>((inner, provider) =>
            {
                var options = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

                if (!options.IsEnabled)
                    return inner;  // Return unwrapped service if no policies are enabled

                var policyFactory = provider.GetRequiredService<IResiliencePolicyFactory>();
                var logger = provider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<GoogleSheetsServiceWithResilience>();
                var policy = policyFactory.CreatePolicy("GoogleSheets");

                return new GoogleSheetsServiceWithResilience(inner, policy, logger);
            });

            return services;
        }

        /// <summary>
        /// Decorator pattern implementation for optional resilience wrapping.
        /// </summary>
        private static IServiceCollection Decorate<TInterface>(
            this IServiceCollection services,
            Func<TInterface, IServiceProvider, TInterface> decorator)
            where TInterface : class
        {
            var wrappedDescriptor = services.FirstOrDefault(
                s => s.ServiceType == typeof(TInterface)) ??
                throw new InvalidOperationException($"{typeof(TInterface).Name} is not registered");

            var objectFactory = ActivatorUtilities.CreateFactory(
                wrappedDescriptor.ImplementationType ?? throw new InvalidOperationException(),
                new[] { typeof(TInterface) });

            services.Replace(ServiceDescriptor.Describe(
                typeof(TInterface),
                provider => decorator(
                    (TInterface)provider.GetRequiredService(wrappedDescriptor.ImplementationType!),
                    provider),
                wrappedDescriptor.Lifetime));

            return services;
        }

        // Existing extension methods remain unchanged
        public static IList<IList<object>> ToGoogleSheetsValues<T>(this IEnumerable<T> list)
        {
            var properties = typeof(T).GetProperties();
            return list.Select(p => properties.Select(prop => prop.GetValue(p)).ToList())
                .Cast<IList<object>>()
                .ToList();
        }
    }
}
```

---

## 4. Usage Examples

### 4.1 Enable Resilience (Default)

```csharp
// Program.cs
var builder = WebApplicationBuilder.CreateBuilder(args);

// Register with resilience enabled (reads from config)
builder.Services.AddGoogleSheetsService(builder.Configuration);

var app = builder.Build();

// In your controller/service
app.MapPost("/api/sync-sheets", async (IGoogleSheetsService service) =>
{
    // Resilience applied automatically via decorator pattern
    var result = await service.ReadSheetAsync(
        spreadsheetId: "abc123",
        sheetName: "Data",
        range: "A1:Z1000"
    );
    return Results.Ok(result);
});
```

### 4.2 Disable Resilience

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Enabled": false
    }
  }
}
```

### 4.3 Configure Specific Policies

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 5,
        "InitialDelayMs": 200,
        "MaxDelayMs": 10000,
        "BackoffMultiplier": 2.5
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 10,
        "CircuitTimeoutSeconds": 60
      },
      "Timeout": {
        "Enabled": true,
        "TimeoutSeconds": 45
      },
      "Bulkhead": {
        "Enabled": false
      }
    }
  }
}
```

### 4.3a Fine-Grained Policy Selection

Clients can enable/disable individual policies independently. Each policy is **disabled by default** unless explicitly enabled.

**Example 1: Only Retry Policy**

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 3
      },
      "CircuitBreaker": { "Enabled": false },
      "Timeout": { "Enabled": false },
      "Bulkhead": { "Enabled": false }
    }
  }
}
```

**Example 2: Retry + Circuit Breaker (No Timeout, No Bulkhead)**

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 3,
        "InitialDelayMs": 100
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 5,
        "CircuitTimeoutSeconds": 30
      },
      "Timeout": { "Enabled": false },
      "Bulkhead": { "Enabled": false }
    }
  }
}
```

**Example 3: All Policies Enabled**

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 5
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 10
      },
      "Timeout": {
        "Enabled": true,
        "TimeoutSeconds": 45
      },
      "Bulkhead": {
        "Enabled": true,
        "MaxParallelization": 20
      }
    }
  }
}
```

**Example 4: Minimal Config (Only Essential Settings)**

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": { "Enabled": true }
    }
  }
}
```

_Note: Circuit Breaker, Timeout, and Bulkhead default to disabled_

### 4.4 Monitor Metrics

```csharp
// In a health check endpoint
app.MapGet("/health/resilience", (IResilienceTelemetry telemetry) =>
{
    var metrics = telemetry.GetMetrics();
    return Results.Ok(new
    {
        metrics.SuccessCount,
        metrics.RetryCount,
        metrics.CircuitBreakerTrippedCount,
        metrics.TimeoutCount,
        successRate = metrics.SuccessRate.ToString("P"),
        totalAttempts = metrics.TotalAttempts
    });
});
```

---

## 5. Implementation Phases

### Phase 1: Core Infrastructure (Week 1)

- [ ] NuGet: Add Polly reference to GoogleSheetsService.csproj
- [ ] Create `ResilienceOptions` classes
- [ ] Create `IResiliencePolicyFactory` and implementation
- [ ] Create `GoogleSheetsServiceWithResilience` decorator

### Phase 2: Integration (Week 2)

- [ ] Update `Extensions.cs` with DI registration and decorator pattern
- [ ] Add `IResilienceTelemetry` for metrics collection
- [ ] Update README with configuration examples
- [ ] Add fluent configuration methods (optional)

### Phase 3: Testing (Week 2-3)

- [ ] Unit tests for each policy
- [ ] Integration tests with mock Google API
- [ ] Multi-threaded stress tests
- [ ] Configuration validation tests

### Phase 4: Documentation & Release (Week 3)

- [ ] Update NuGet package documentation
- [ ] Add migration guide for existing consumers
- [ ] Version bump (1.1.0 → 1.2.0)
- [ ] Publish updated package

---

## 6. Backward Compatibility

**This implementation maintains 100% backward compatibility:**

1. **Default State**:
   - No top-level `Resilience.Enabled` flag needed
   - **Resilience is automatically enabled if any policy is enabled** (derived from individual policies)
   - **All individual policies default to disabled** unless explicitly enabled
   - This means opt-in behavior for each policy (pick and choose)

2. **Opt-Out**:
   - Disable all resilience: Don't enable any policies (leave all at `Enabled = false`)
   - Disable specific policies: Set each policy's `Enabled = false`

3. **Existing Code**: No changes required to existing client code

4. **Graceful Degradation**:
   - If not configured, all policies are disabled—zero resilience overhead (safe default)
   - Clients must explicitly enable the policies they need

### Migration Path for Existing Consumers

**Scenario 1: Consumers upgrading with no configuration**

```csharp
// OLD (still works as-is)
services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();

// NEW (recommended - gives control)
services.AddGoogleSheetsService(configuration);

// Result: Framework enabled only if policies are configured/enabled
```

**Scenario 2: Disable all resilience**

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": { "Enabled": false },
      "CircuitBreaker": { "Enabled": false },
      "Timeout": { "Enabled": false },
      "Bulkhead": { "Enabled": false }
    }
  }
}
```

_Or simply don't configure the Resilience section at all_

**Scenario 3: Enable specific policies**

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": { "Enabled": true },
      "CircuitBreaker": { "Enabled": true }
    }
  }
}
```

_Timeout and Bulkhead remain disabled (default)_

### Default Behavior Table

| Configuration                  | Retry | CircuitBreaker | Timeout | Bulkhead | Framework Active |
| ------------------------------ | ----- | -------------- | ------- | -------- | ---------------- |
| Not configured                 | ❌    | ❌             | ❌      | ❌       | ❌               |
| Empty Resilience object        | ❌    | ❌             | ❌      | ❌       | ❌               |
| `Retry.Enabled = true` only    | ✅    | ❌             | ❌      | ❌       | ✅               |
| Retry + CircuitBreaker enabled | ✅    | ✅             | ❌      | ❌       | ✅               |
| All policies `Enabled = true`  | ✅    | ✅             | ✅      | ✅       | ✅               |

---

## 7. NuGet Dependencies

**Update GoogleSheetsService.csproj:**

```xml
<ItemGroup>
    <PackageReference Include="Google.Apis.Sheets.v4" Version="1.60.0.2979" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.1" />
    <PackageReference Include="Polly" Version="8.2.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
</ItemGroup>
```

---

## 8. Testing Strategy

### Unit Tests

```csharp
// ResiliencePolicyFactoryTests.cs
[TestClass]
public class ResiliencePolicyFactoryTests
{
    [TestMethod]
    public void CreatePolicy_WhenDisabled_ReturnsNoOpPolicy()
    {
        var options = new ResilienceOptions { Enabled = false };
        var factory = new ResiliencePolicyFactory(options, LoggerMock.Create());
        var policy = factory.CreatePolicy("GoogleSheets");

        // Should execute without retry/circuit breaker logic
    }

    [TestMethod]
    public async Task RetryPolicy_RetriesOnFailure()
    {
        var options = new ResilienceOptions
        {
            Enabled = true,
            Retry = { Enabled = true, MaxRetries = 3 }
        };
        var factory = new ResiliencePolicyFactory(options, LoggerMock.Create());
        var policy = factory.CreatePolicy("GoogleSheets");

        var attemptCount = 0;
        await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new HttpRequestException("Transient failure");
        });

        Assert.AreEqual(3, attemptCount);
    }

    [TestMethod]
    public async Task CircuitBreaker_TripsAfterThreshold()
    {
        var options = new ResilienceOptions
        {
            Enabled = true,
            CircuitBreaker = { Enabled = true, FailureThreshold = 3 }
        };
        var factory = new ResiliencePolicyFactory(options, LoggerMock.Create());
        var policy = factory.CreatePolicy("GoogleSheets");

        // Should trip after 3 failures
    }
}
```

### Integration Tests

```csharp
// GoogleSheetsServiceResilienceIntegrationTests.cs
[TestClass]
public class GoogleSheetsServiceResilienceIntegrationTests
{
    [TestMethod]
    public async Task ReadSheet_WithResilience_SucceedsAfterTransientFailure()
    {
        // Setup mock Google API that fails once then succeeds
        var mockApi = MockGoogleSheetsApi.Create()
            .WithFailureOnce(new HttpRequestException("Transient"))
            .WithSuccessOn(attempt: 2);

        var services = new ServiceCollection()
            .AddGoogleSheetsService(BuildConfiguration(enableResilience: true))
            .AddSingleton(mockApi.SheetsService)
            .BuildServiceProvider();

        var service = services.GetRequiredService<IGoogleSheetsService>();
        var result = await service.ReadSheetAsync("id", "Sheet1", "A1:Z100");

        Assert.IsNotNull(result);
    }
}
```

---

## 9. Configuration Best Practices

### Development

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": { "Enabled": false },
      "CircuitBreaker": { "Enabled": false },
      "Timeout": { "Enabled": false },
      "Bulkhead": { "Enabled": false }
    }
  }
}
```

_All policies disabled for easier debugging (or omit the Resilience section entirely)_

### Staging

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 2
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 10
      },
      "Timeout": { "Enabled": false },
      "Bulkhead": { "Enabled": false }
    }
  }
}
```

_Retry + Circuit Breaker enabled. Timeout and Bulkhead disabled to keep staging simple_

### Production (Conservative)

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 3,
        "InitialDelayMs": 100,
        "MaxDelayMs": 5000
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 5,
        "CircuitTimeoutSeconds": 60
      },
      "Timeout": { "Enabled": false },
      "Bulkhead": { "Enabled": false }
    }
  }
}
```

_Balanced approach: Retry + Circuit Breaker for resilience without resource limits_

### Production (Full Protection)

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 5,
        "InitialDelayMs": 200,
        "MaxDelayMs": 10000,
        "BackoffMultiplier": 2.5
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 5,
        "CircuitTimeoutSeconds": 60
      },
      "Timeout": {
        "Enabled": true,
        "TimeoutSeconds": 45
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

_All policies enabled for maximum resilience. Use when high-traffic or strict SLAs_

### High-Throughput API

```json
{
  "GoogleSheetsService": {
    "Resilience": {
      "Retry": {
        "Enabled": true,
        "MaxRetries": 2,
        "InitialDelayMs": 50,
        "MaxDelayMs": 2000
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 3,
        "CircuitTimeoutSeconds": 30
      },
      "Timeout": {
        "Enabled": true,
        "TimeoutSeconds": 20
      },
      "Bulkhead": {
        "Enabled": true,
        "MaxParallelization": 100,
        "MaxQueueingActions": 200
      }
    }
  }
}
```

_Aggressive config: Fast retries, tight timeouts, high concurrency for APIs_
