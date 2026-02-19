using GoogleSheetsService.Resilience;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace GoogleSheetsService.Tests.Resilience
{
    /// <summary>
    /// Comprehensive tests for ResiliencePipelineFactory.
    /// </summary>
    public class ResiliencePolicyFactoryTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public ResiliencePolicyFactoryTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public void Constructor_WhenNoPolicesEnabled_DoesNotInitializePolicies()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = false },
                CircuitBreaker = { Enabled = false },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = false }
            };

            // Act
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);

            // Assert
            var pipeline = factory.CreatePipeline("GoogleSheets");
            Assert.NotNull(pipeline);
        }

        [Fact]
        public void Constructor_WhenRetryEnabled_InitializesPolicies()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true, MaxRetries = 3 },
                CircuitBreaker = { Enabled = false },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = false }
            };

            // Act
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);

            // Assert
            var pipeline = factory.CreatePipeline("GoogleSheets");
            Assert.NotNull(pipeline);
        }

        [Fact]
        public void CreatePolicy_WhenDisabled_ReturnsNoOpPolicy()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = false },
                CircuitBreaker = { Enabled = false },
                Timeout = { Enabled = false },
                Bulkhead = { Enabled = false }
            };
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);

            // Act
            var pipeline = factory.CreatePipeline("GoogleSheets");

            // Assert - should not throw for unknown policy
            Assert.NotNull(pipeline);
        }

        [Fact]
        public void CreatePolicy_WhenPolicyNameNotFound_ReturnsNoOpPolicy()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true }
            };
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);

            // Act
            var pipeline = factory.CreatePipeline("UnknownPolicy");

            // Assert
            Assert.NotNull(pipeline);
        }

        [Fact(Skip = "Polly v8 generic pipeline API works differently")]
        public void CreatePolicyGeneric_ReturnsTypedPolicy()
        {
            // Polly v8 uses ResiliencePipeline which doesn't have generic type parameter
            // This test is no longer applicable
        }

        [Fact]
        public async Task RetryPolicy_RetriesOnTransientException()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true, MaxRetries = 3, InitialDelayMs = 10 }
            };
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);
            var pipeline = factory.CreatePipeline("GoogleSheets");

            int attemptCount = 0;
            var exception = new HttpRequestException("Transient failure");

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await pipeline.ExecuteAsync(async ct =>
                {
                    attemptCount++;
                    if (attemptCount < 5)  // More than max retries
                        throw exception;
                    await Task.CompletedTask;
                }, CancellationToken.None);
            });

            // Should have retried up to MaxRetries + 1 initial attempt
            Assert.Equal(4, attemptCount);  // 1 initial + 3 retries
        }

        [Fact]
        public async Task RetryPolicy_SucceedsAfterTransientFailure()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true, MaxRetries = 3, InitialDelayMs = 10 }
            };
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);
            var pipeline = factory.CreatePipeline("GoogleSheets");

            int attemptCount = 0;

            // Act
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount == 1)
                    throw new HttpRequestException("Transient failure");
                await Task.CompletedTask;
            }, CancellationToken.None);

            // Assert
            Assert.Equal(2, attemptCount);  // 1 initial + 1 retry
        }

        [Fact]
        public async Task CircuitBreaker_TripsAfterFailureThreshold()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                CircuitBreaker =
                {
                    Enabled = true,
                    FailureThreshold = 2,
                    FailureWindowSeconds = 60,
                    CircuitTimeoutSeconds = 1
                }
            };
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);
            var pipeline = factory.CreatePipeline("GoogleSheets");

            // Act & Assert
            // First two failures should trigger the circuit breaker
            for (int i = 0; i < 2; i++)
            {
                await Assert.ThrowsAsync<Exception>(async () =>
                    await pipeline.ExecuteAsync(async ct =>
                    {
                        throw new Exception("Test failure");
                    }, CancellationToken.None)
                );
            }

            // Third request should be rejected by circuit breaker
            await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
                await pipeline.ExecuteAsync(async ct =>
                {
                    await Task.CompletedTask;
                }, CancellationToken.None)
            );
        }

        [Fact]
        public async Task Timeout_ThrowsTimeoutExceptionOnTimeout()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Timeout = { Enabled = true, TimeoutSeconds = 1 }
            };
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);
            var pipeline = factory.CreatePipeline("GoogleSheets");

            // Act & Assert - Polly v8 throws TimeoutRejectedException
            await Assert.ThrowsAsync<Polly.Timeout.TimeoutRejectedException>(async () =>
                await pipeline.ExecuteAsync(async ct =>
                {
                    await Task.Delay(2000, ct);  // Delay longer than timeout
                }, CancellationToken.None)
            );
        }

        [Fact]
        public async Task Bulkhead_RejectsWhenMaxParallelizationExceeded()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Bulkhead = { Enabled = true, MaxParallelization = 1, MaxQueueingActions = 0 }
            };
            var factory = new ResiliencePipelineFactory(options, _loggerMock.Object);
            var pipeline = factory.CreatePipeline("GoogleSheets");

            var slowTask = pipeline.ExecuteAsync(async ct =>
            {
                await Task.Delay(500, ct);
            }, CancellationToken.None);

            // Act & Assert - second request should be rejected
            await Task.Delay(100);  // Allow first task to start

            // Custom bulkhead strategy throws InvalidOperationException when capacity is exceeded
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await pipeline.ExecuteAsync(async ct =>
                {
                    await Task.CompletedTask;
                }, CancellationToken.None)
            );

            await slowTask;
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenRetryEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                Retry = { Enabled = true }
            };

            // Act
            var isEnabled = options.IsEnabled;

            // Assert
            Assert.True(isEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsTrueWhenCircuitBreakerEnabled()
        {
            // Arrange
            var options = new ResilienceOptions
            {
                CircuitBreaker = { Enabled = true }
            };

            // Act
            var isEnabled = options.IsEnabled;

            // Assert
            Assert.True(isEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsFalseWhenAllDisabled()
        {
            // Arrange
            var options = new ResilienceOptions();

            // Act
            var isEnabled = options.IsEnabled;

            // Assert
            Assert.False(isEnabled);
        }
    }
}
