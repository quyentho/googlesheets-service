using GoogleSheetsService.Resilience;
using Xunit;

namespace GoogleSheetsService.Tests.Resilience
{
    /// <summary>
    /// Tests for resilience configuration options.
    /// </summary>
    public class ResilienceOptionsTests
    {
        [Fact]
        public void ResilienceOptions_HasCorrectSectionKey()
        {
            // Act & Assert
            Assert.Equal("GoogleSheetsService:Resilience", ResilienceOptions.SectionKey);
        }

        [Fact]
        public void ResilienceOptions_DefaultConstructor_InitializesSubOptions()
        {
            // Act
            var options = new ResilienceOptions();

            // Assert
            Assert.NotNull(options.Retry);
            Assert.NotNull(options.CircuitBreaker);
            Assert.NotNull(options.Timeout);
            Assert.NotNull(options.Bulkhead);
            Assert.NotNull(options.Telemetry);
        }

        [Fact]
        public void RetryOptions_HasCorrectDefaults()
        {
            // Act
            var options = new RetryOptions();

            // Assert
            Assert.False(options.Enabled);
            Assert.Equal(3, options.MaxRetries);
            Assert.Equal(100, options.InitialDelayMs);
            Assert.Equal(5000, options.MaxDelayMs);
            Assert.Equal(2.0, options.BackoffMultiplier);
        }

        [Fact]
        public void CircuitBreakerOptions_HasCorrectDefaults()
        {
            // Act
            var options = new CircuitBreakerOptions();

            // Assert
            Assert.False(options.Enabled);
            Assert.Equal(5, options.FailureThreshold);
            Assert.Equal(60, options.FailureWindowSeconds);
            Assert.Equal(2, options.SuccessThresholdInHalfOpenState);
            Assert.Equal(30, options.CircuitTimeoutSeconds);
        }

        [Fact]
        public void TimeoutOptions_HasCorrectDefaults()
        {
            // Act
            var options = new TimeoutOptions();

            // Assert
            Assert.False(options.Enabled);
            Assert.Equal(30, options.TimeoutSeconds);
        }

        [Fact]
        public void BulkheadOptions_HasCorrectDefaults()
        {
            // Act
            var options = new BulkheadOptions();

            // Assert
            Assert.False(options.Enabled);
            Assert.Equal(20, options.MaxParallelization);
            Assert.Equal(50, options.MaxQueueingActions);
        }

        [Fact]
        public void TelemetryOptions_HasCorrectDefaults()
        {
            // Act
            var options = new TelemetryOptions();

            // Assert
            Assert.True(options.Enabled);
            Assert.Equal("Information", options.LogLevel);
        }

        [Fact]
        public void IsEnabled_ReturnsFalseWhenNoPolicesEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = false },
                CircuitBreaker = { Enabled = false },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = false }
            };

            // Act & Assert
            Assert.False(options.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenRetryEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true },
                CircuitBreaker = { Enabled = false },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = false }
            };

            // Act & Assert
            Assert.True(options.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenCircuitBreakerEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = false },
                CircuitBreaker = { Enabled = true },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = false }
            };

            // Act & Assert
            Assert.True(options.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenTimeoutEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = false },
                CircuitBreaker = { Enabled = false },
                Timeout = { Enabled = true },
                Bulkhead = { Enabled = false }
            };

            // Act & Assert
            Assert.True(options.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenBulkheadEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = false },
                CircuitBreaker = { Enabled = false },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = true }
            };

            // Act & Assert
            Assert.True(options.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenMultiplePoliciesEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true },
                CircuitBreaker = { Enabled = true },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = false }
            };

            // Act & Assert
            Assert.True(options.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenAllPoliciesEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true },
                CircuitBreaker = { Enabled = true },
                Timeout = { Enabled = true },
                Bulkhead = { Enabled = true }
            };

            // Act & Assert
            Assert.True(options.IsEnabled);
        }

        [Fact]
        public void CanModifyPolicyOptions()
        {
            // Arrange
            var options = new ResilienceOptions();

            // Act
            options.Retry.Enabled = true;
            options.Retry.MaxRetries = 5;
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.FailureThreshold = 10;

            // Assert
            Assert.True(options.Retry.Enabled);
            Assert.Equal(5, options.Retry.MaxRetries);
            Assert.True(options.CircuitBreaker.Enabled);
            Assert.Equal(10, options.CircuitBreaker.FailureThreshold);
            Assert.True(options.IsEnabled);
        }
    }
}
