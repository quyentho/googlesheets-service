namespace GoogleSheetsService.Resilience
{
    /// <summary>
    /// Root configuration for Google Sheets Service resilience policies.
    /// </summary>
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

    /// <summary>
    /// Configuration for the retry policy with exponential backoff.
    /// </summary>
    public class RetryOptions
    {
        /// <summary>
        /// Enable retry policy with exponential backoff and jitter.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Maximum number of retry attempts.
        /// Default: 3
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Initial delay for the first retry in milliseconds.
        /// Default: 100
        /// </summary>
        public int InitialDelayMs { get; set; } = 100;

        /// <summary>
        /// Maximum delay between retries in milliseconds.
        /// Default: 5000
        /// </summary>
        public int MaxDelayMs { get; set; } = 5000;

        /// <summary>
        /// Backoff multiplier for exponential backoff strategy.
        /// Default: 2.0
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;
    }

    /// <summary>
    /// Configuration for the circuit breaker policy.
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// Enable circuit breaker to prevent cascading failures.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Number of failures that trigger the circuit breaker.
        /// Default: 5
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Time window within which failures are counted in seconds.
        /// Default: 60
        /// </summary>
        public int FailureWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Number of successful calls to close the circuit when in half-open state.
        /// Default: 2
        /// </summary>
        public int SuccessThresholdInHalfOpenState { get; set; } = 2;

        /// <summary>
        /// Duration for which the circuit remains open before transitioning to half-open in seconds.
        /// Default: 30
        /// </summary>
        public int CircuitTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Configuration for the timeout policy.
    /// </summary>
    public class TimeoutOptions
    {
        /// <summary>
        /// Enable timeout policy to prevent hanging requests.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Timeout duration in seconds.
        /// Default: 30
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Configuration for the bulkhead isolation policy.
    /// </summary>
    public class BulkheadOptions
    {
        /// <summary>
        /// Enable bulkhead policy to limit concurrent operations.
        /// Default: false (disabled unless explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Maximum number of concurrent executions.
        /// Default: 20
        /// </summary>
        public int MaxParallelization { get; set; } = 20;

        /// <summary>
        /// Maximum number of queued actions when max parallelization is reached.
        /// Default: 50
        /// </summary>
        public int MaxQueueingActions { get; set; } = 50;
    }

    /// <summary>
    /// Configuration for resilience telemetry and logging.
    /// </summary>
    public class TelemetryOptions
    {
        /// <summary>
        /// Enable telemetry collection and logging.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Log level for resilience operations (Information, Warning, Error, etc).
        /// Default: "Information"
        /// </summary>
        public string LogLevel { get; set; } = "Information";
    }
}
