namespace GoogleSheetsService.Resilience;

using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Polly.Retry;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory for creating Polly resilience pipelines.
/// </summary>
public interface IResiliencePipelineFactory
{
    /// <summary>
    /// Creates a resilience pipeline for a given policy name.
    /// </summary>
    ResiliencePipeline CreatePipeline(string pipelineName);
}

/// <summary>
/// Factory implementation for creating Polly resilience policies.
/// </summary>
public class ResiliencePipelineFactory : IResiliencePipelineFactory
{
    private readonly ResilienceOptions _options;
    private readonly ILogger _logger;
    private readonly Dictionary<string, ResiliencePipeline> _pipelines = new();

    public ResiliencePipelineFactory(ResilienceOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        if (options.IsEnabled)
        {
            _logger.LogInformation("Initializing Polly resilience pipelines");
            InitializePipelines();
        }
    }

    private void InitializePipelines()
    {
        var builder = new ResiliencePipelineBuilder();

        // Add strategies in order: outer to inner
        // Timeout wraps everything
        if (_options.Timeout.Enabled)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(_options.Timeout.TimeoutSeconds),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Operation timed out after {Timeout}s",
                        _options.Timeout.TimeoutSeconds
                    );
                    return default;
                }
            });
        }

        // Bulkhead limits concurrency using a semaphore-based strategy
        if (_options.Bulkhead.Enabled)
        {
            var semaphore = new SemaphoreSlim(_options.Bulkhead.MaxParallelization, _options.Bulkhead.MaxParallelization);
            builder.AddStrategy(_ => new BulkheadResilienceStrategy(semaphore, _logger), new BulkheadResilienceStrategyOptions());
        }

        // Circuit breaker prevents cascading failures
        if (_options.CircuitBreaker.Enabled)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(_options.CircuitBreaker.FailureWindowSeconds),
                MinimumThroughput = _options.CircuitBreaker.FailureThreshold,
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreaker.CircuitTimeoutSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Circuit breaker opened. Break duration: {BreakDuration}s",
                        _options.CircuitBreaker.CircuitTimeoutSeconds
                    );
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker reset to closed state");
                    return default;
                }
            });
        }

        // Retry is innermost
        if (_options.Retry.Enabled)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.Retry.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.Retry.InitialDelayMs),
                MaxDelay = TimeSpan.FromMilliseconds(_options.Retry.MaxDelayMs),
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry attempt {AttemptNumber} after {RetryDelay}ms. Exception: {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message
                    );
                    return default;
                }
            });
        }

        _pipelines["GoogleSheets"] = builder.Build();

        _logger.LogInformation(
            "Resilience pipelines initialized successfully. " +
            "Retry: {RetryEnabled}, CircuitBreaker: {CBEnabled}, Timeout: {TOEnabled}, Bulkhead: {BHEnabled}",
            _options.Retry.Enabled,
            _options.CircuitBreaker.Enabled,
            _options.Timeout.Enabled,
            _options.Bulkhead.Enabled
        );
    }

    public ResiliencePipeline CreatePipeline(string pipelineName)
    {
        if (!_options.IsEnabled)
            return ResiliencePipeline.Empty;

        return _pipelines.TryGetValue(pipelineName, out var pipeline)
            ? pipeline
            : ResiliencePipeline.Empty;
    }
}

/// <summary>
/// Options for the custom bulkhead (concurrency limiting) resilience strategy.
/// </summary>
internal sealed class BulkheadResilienceStrategyOptions : ResilienceStrategyOptions
{
    public BulkheadResilienceStrategyOptions()
    {
        Name = "Bulkhead";
    }
}

/// <summary>
/// Custom resilience strategy that limits concurrency using a SemaphoreSlim (bulkhead pattern).
/// </summary>
internal sealed class BulkheadResilienceStrategy : ResilienceStrategy
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _logger;

    public BulkheadResilienceStrategy(SemaphoreSlim semaphore, ILogger logger)
    {
        _semaphore = semaphore;
        _logger = logger;
    }

    protected override async ValueTask<Outcome<TResult>> ExecuteCore<TResult, TState>(
        Func<ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback,
        ResilienceContext context,
        TState state)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.Zero, context.CancellationToken))
        {
            _logger.LogWarning("Bulkhead capacity exceeded. Request rejected.");
            return Outcome.FromException<TResult>(
                new InvalidOperationException("Bulkhead capacity exceeded. Too many concurrent operations."));
        }

        try
        {
            return await callback(context, state);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
