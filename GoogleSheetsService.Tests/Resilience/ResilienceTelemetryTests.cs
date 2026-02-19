using GoogleSheetsService.Resilience;
using Xunit;

namespace GoogleSheetsService.Tests.Resilience
{
    /// <summary>
    /// Tests for ResilienceTelemetry and ResilienceMetrics.
    /// </summary>
    public class ResilienceTelemetryTests
    {
        [Fact]
        public void GetMetrics_InitiallyReturnsZeroCounts()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();

            // Act
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(0, metrics.SuccessCount);
            Assert.Equal(0, metrics.RetryCount);
            Assert.Equal(0, metrics.CircuitBreakerTrippedCount);
            Assert.Equal(0, metrics.TimeoutCount);
            Assert.Equal(0, metrics.BulkheadRejectionCount);
        }

        [Fact]
        public void IncrementSuccess_IncreasesCounter()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();

            // Act
            telemetry.IncrementSuccess();
            telemetry.IncrementSuccess();
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(2, metrics.SuccessCount);
        }

        [Fact]
        public void IncrementRetry_IncreasesCounter()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();

            // Act
            telemetry.IncrementRetry();
            telemetry.IncrementRetry();
            telemetry.IncrementRetry();
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(3, metrics.RetryCount);
        }

        [Fact]
        public void IncrementCircuitBreakerTripped_IncreasesCounter()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();

            // Act
            telemetry.IncrementCircuitBreakerTripped();
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(1, metrics.CircuitBreakerTrippedCount);
        }

        [Fact]
        public void IncrementTimeout_IncreasesCounter()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();

            // Act
            telemetry.IncrementTimeout();
            telemetry.IncrementTimeout();
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(2, metrics.TimeoutCount);
        }

        [Fact]
        public void IncrementBulkheadRejection_IncreasesCounter()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();

            // Act
            telemetry.IncrementBulkheadRejection();
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(1, metrics.BulkheadRejectionCount);
        }

        [Fact]
        public void Reset_ClearsAllCounters()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();
            telemetry.IncrementCircuitBreakerTripped();

            // Act
            telemetry.Reset();
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(0, metrics.SuccessCount);
            Assert.Equal(0, metrics.RetryCount);
            Assert.Equal(0, metrics.CircuitBreakerTrippedCount);
            Assert.Equal(0, metrics.TimeoutCount);
            Assert.Equal(0, metrics.BulkheadRejectionCount);
        }

        [Fact]
        public void TotalAttempts_CalculatesCorrectly()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();
            telemetry.IncrementSuccess();
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();
            telemetry.IncrementCircuitBreakerTripped();
            telemetry.IncrementTimeout();

            // Act
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(5, metrics.TotalAttempts);
        }

        [Fact]
        public void SuccessRate_CalculatesCorrectly()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();
            telemetry.IncrementSuccess();
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();

            // Act
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(2.0 / 3.0, metrics.SuccessRate);
        }

        [Fact]
        public void SuccessRate_ReturnsZeroWhenNoAttempts()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();

            // Act
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(0.0, metrics.SuccessRate);
        }

        [Fact]
        public void SuccessRatePercentage_FormatsCorrectly()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();

            // Act
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal("50.00%", metrics.SuccessRatePercentage);
        }

        [Fact]
        public void Counters_AreThreadSafe()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();
            const int threadCount = 10;
            const int incrementsPerThread = 100;
            var tasks = new Task[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < incrementsPerThread; j++)
                    {
                        telemetry.IncrementSuccess();
                    }
                });
            }

            Task.WaitAll(tasks);
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(threadCount * incrementsPerThread, metrics.SuccessCount);
        }

        [Fact]
        public void MetricsSnapshot_IsIndependent()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();
            telemetry.IncrementSuccess();
            var metrics1 = telemetry.GetMetrics();

            // Act
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();
            var metrics2 = telemetry.GetMetrics();

            // Assert
            Assert.Equal(1, metrics1.SuccessCount);
            Assert.Equal(2, metrics2.SuccessCount);
        }

        [Fact]
        public void MultipleMetricsTypes_CalculatedCorrectly()
        {
            // Arrange
            var telemetry = new ResilienceTelemetry();
            telemetry.IncrementSuccess();
            telemetry.IncrementSuccess();
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();
            telemetry.IncrementCircuitBreakerTripped();
            telemetry.IncrementTimeout();
            telemetry.IncrementBulkheadRejection();

            // Act
            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(3, metrics.SuccessCount);
            Assert.Equal(1, metrics.RetryCount);
            Assert.Equal(1, metrics.CircuitBreakerTrippedCount);
            Assert.Equal(1, metrics.TimeoutCount);
            Assert.Equal(1, metrics.BulkheadRejectionCount);
            Assert.Equal(7, metrics.TotalAttempts);
            Assert.Equal(3.0 / 7.0, metrics.SuccessRate);
        }
    }
}
