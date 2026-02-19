namespace GoogleSheetsService.Resilience
{
    /// <summary>
    /// Interface for tracking resilience policy metrics.
    /// </summary>
    public interface IResilienceTelemetry
    {
        /// <summary>
        /// Gets the current resilience metrics.
        /// </summary>
        ResilienceMetrics GetMetrics();

        /// <summary>
        /// Resets all metrics to zero.
        /// </summary>
        void Reset();

        /// <summary>
        /// Increments the success counter.
        /// </summary>
        void IncrementSuccess();

        /// <summary>
        /// Increments the retry counter.
        /// </summary>
        void IncrementRetry();

        /// <summary>
        /// Increments the circuit breaker tripped counter.
        /// </summary>
        void IncrementCircuitBreakerTripped();

        /// <summary>
        /// Increments the timeout counter.
        /// </summary>
        void IncrementTimeout();

        /// <summary>
        /// Increments the bulkhead rejection counter.
        /// </summary>
        void IncrementBulkheadRejection();
    }

    /// <summary>
    /// Tracks resilience policy metrics using thread-safe counters.
    /// </summary>
    public class ResilienceTelemetry : IResilienceTelemetry
    {
        private int _successCount;
        private int _retryCount;
        private int _circuitBreakerTrippedCount;
        private int _timeoutCount;
        private int _bulkheadRejectionCount;

        /// <summary>
        /// Increments the success counter.
        /// </summary>
        public void IncrementSuccess() => Interlocked.Increment(ref _successCount);

        /// <summary>
        /// Increments the retry counter.
        /// </summary>
        public void IncrementRetry() => Interlocked.Increment(ref _retryCount);

        /// <summary>
        /// Increments the circuit breaker tripped counter.
        /// </summary>
        public void IncrementCircuitBreakerTripped() => Interlocked.Increment(ref _circuitBreakerTrippedCount);

        /// <summary>
        /// Increments the timeout counter.
        /// </summary>
        public void IncrementTimeout() => Interlocked.Increment(ref _timeoutCount);

        /// <summary>
        /// Increments the bulkhead rejection counter.
        /// </summary>
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

    /// <summary>
    /// Immutable snapshot of resilience metrics.
    /// </summary>
    public class ResilienceMetrics
    {
        /// <summary>
        /// Number of successful operations.
        /// </summary>
        public int SuccessCount { get; init; }

        /// <summary>
        /// Number of retry attempts.
        /// </summary>
        public int RetryCount { get; init; }

        /// <summary>
        /// Number of times the circuit breaker was tripped.
        /// </summary>
        public int CircuitBreakerTrippedCount { get; init; }

        /// <summary>
        /// Number of operations that timed out.
        /// </summary>
        public int TimeoutCount { get; init; }

        /// <summary>
        /// Number of bulkhead rejections.
        /// </summary>
        public int BulkheadRejectionCount { get; init; }

        /// <summary>
        /// Total number of attempts (including retries).
        /// </summary>
        public int TotalAttempts => SuccessCount + RetryCount + CircuitBreakerTrippedCount + TimeoutCount + BulkheadRejectionCount;

        /// <summary>
        /// Success rate as a decimal (0.0 to 1.0).
        /// </summary>
        public double SuccessRate => TotalAttempts > 0 ? (double)SuccessCount / TotalAttempts : 0;

        /// <summary>
        /// Success rate as a percentage string.
        /// </summary>
        public string SuccessRatePercentage => SuccessRate.ToString("P2");
    }
}
