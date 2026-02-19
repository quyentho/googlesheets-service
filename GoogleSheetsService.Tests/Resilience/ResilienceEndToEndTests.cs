using GoogleSheetsService.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GoogleSheetsService.Tests.Resilience
{
    /// <summary>
    /// End-to-end integration tests for resilience with decorator pattern.
    /// </summary>
    public class ResilienceEndToEndTests
    {
        private IConfigurationBuilder CreateConfigurationBuilder()
        {
            return new ConfigurationBuilder();
        }

        private ServiceProvider SetupServicesWithResilience(IConfiguration config, IGoogleSheetsService mockService)
        {
            var services = new ServiceCollection();
            services.AddLogging();

            // Configure resilience options
            var resilienceSection = config.GetSection(ResilienceOptions.SectionKey);
            services.Configure<ResilienceOptions>(resilienceSection);

            // Register resilience components
            services.AddSingleton<IResiliencePipelineFactory>(provider =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>().Value;
                var logger = provider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GoogleSheetsService.Resilience");
                return new ResiliencePipelineFactory(options, logger);
            });

            // Register telemetry
            services.AddSingleton<IResilienceTelemetry, ResilienceTelemetry>();

            // Register the decorated service
            services.AddScoped<IGoogleSheetsService>(provider =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>().Value;

                if (!options.IsEnabled)
                    return mockService;  // Return unwrapped service if no policies are enabled

                var pipelineFactory = provider.GetRequiredService<IResiliencePipelineFactory>();
                var logger = provider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<GoogleSheetsServiceWithResilience>();
                var pipeline = pipelineFactory.CreatePipeline("GoogleSheets");

                return new GoogleSheetsServiceWithResilience(mockService, pipeline, logger);
            });

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task FullFlow_WithRetryPolicy_RetriesOnTransientFailure()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Retry:MaxRetries", "3" },
                { "GoogleSheetsService:Resilience:Retry:InitialDelayMs", "10" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var innerServiceMock = new Mock<IGoogleSheetsService>();
            var attemptCount = 0;

            innerServiceMock
                .Setup(s => s.ReadSheetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string id, string sheet, string range) =>
                {
                    attemptCount++;
                    if (attemptCount < 2)
                        throw new HttpRequestException("Transient failure");

                    var result = new List<IList<object>>
                    {
                        new List<object> { "Data" }
                    };
                    return Task.FromResult((IList<IList<object>>?)result);
                });

            var provider = SetupServicesWithResilience(config, innerServiceMock.Object);
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            // Act
            var result = await service.ReadSheetAsync("id", "Sheet", "A1:Z");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, attemptCount);  // Should have retried once
        }

        [Fact]
        public async Task FullFlow_WithoutResilience_CallsServiceDirectly()
        {
            // Arrange - no resilience config
            var config = CreateConfigurationBuilder().Build();

            var innerServiceMock = new Mock<IGoogleSheetsService>();
            var attemptCount = 0;

            innerServiceMock
                .Setup(s => s.WriteSheetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<IList<object>>>()))
                .Callback(() => attemptCount++)
                .Returns(Task.CompletedTask);

            var provider = SetupServicesWithResilience(config, innerServiceMock.Object);
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            var values = new List<IList<object>>();

            // Act
            await service.WriteSheetAsync("id", "Sheet", "A1:B1", values);

            // Assert
            Assert.Equal(1, attemptCount);  // Should be called once, no retries
            // Service should be the mock itself, not decorated
            Assert.Same(innerServiceMock.Object, service);
        }

        [Fact]
        public async Task FullFlow_DecoratorPreservesServiceMethods()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var innerServiceMock = new Mock<IGoogleSheetsService>();
            var expectedData = new List<IList<object>>
            {
                new List<object> { "Header1", "Header2" },
                new List<object> { "Value1", "Value2" }
            };

            innerServiceMock
                .Setup(s => s.ReadSheetAsync("123", "Sheet1", "A1:Z100"))
                .ReturnsAsync(expectedData);

            var provider = SetupServicesWithResilience(config, innerServiceMock.Object);
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            // Act
            var result = await service.ReadSheetAsync("123", "Sheet1", "A1:Z100");

            // Assert
            Assert.Equal(expectedData, result);
            Assert.IsType<GoogleSheetsServiceWithResilience>(service);
        }

        [Fact]
        public void FullFlow_TelemetryCanBeMonitored()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var mockService = new Mock<IGoogleSheetsService>().Object;
            var provider = SetupServicesWithResilience(config, mockService);
            var telemetry = provider.GetRequiredService<IResilienceTelemetry>();

            // Act
            telemetry.IncrementSuccess();
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();

            var metrics = telemetry.GetMetrics();

            // Assert
            Assert.Equal(2, metrics.SuccessCount);
            Assert.Equal(1, metrics.RetryCount);
            Assert.Equal(2 / 3.0, metrics.SuccessRate);
        }

        [Fact]
        public void FullFlow_CustomRetryConfiguration()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Retry:MaxRetries", "10" },
                { "GoogleSheetsService:Resilience:Retry:InitialDelayMs", "500" },
                { "GoogleSheetsService:Resilience:Retry:MaxDelayMs", "30000" },
                { "GoogleSheetsService:Resilience:Retry:BackoffMultiplier", "3.0" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var mockService = new Mock<IGoogleSheetsService>().Object;
            var provider = SetupServicesWithResilience(config, mockService);
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>().Value;

            // Assert
            Assert.True(options.Retry.Enabled);
            Assert.Equal(10, options.Retry.MaxRetries);
            Assert.Equal(500, options.Retry.InitialDelayMs);
            Assert.Equal(30000, options.Retry.MaxDelayMs);
            Assert.Equal(3.0, options.Retry.BackoffMultiplier);
        }

        [Fact]
        public void FullFlow_AllPoliciesCanBeConfigured()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Retry:MaxRetries", "5" },
                { "GoogleSheetsService:Resilience:CircuitBreaker:Enabled", "true" },
                { "GoogleSheetsService:Resilience:CircuitBreaker:FailureThreshold", "10" },
                { "GoogleSheetsService:Resilience:CircuitBreaker:CircuitTimeoutSeconds", "60" },
                { "GoogleSheetsService:Resilience:Timeout:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Timeout:TimeoutSeconds", "45" },
                { "GoogleSheetsService:Resilience:Bulkhead:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Bulkhead:MaxParallelization", "100" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var mockService = new Mock<IGoogleSheetsService>().Object;
            var provider = SetupServicesWithResilience(config, mockService);
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>().Value;

            // Assert
            Assert.True(options.IsEnabled);
            Assert.True(options.Retry.Enabled);
            Assert.True(options.CircuitBreaker.Enabled);
            Assert.True(options.Timeout.Enabled);
            Assert.True(options.Bulkhead.Enabled);

            Assert.Equal(5, options.Retry.MaxRetries);
            Assert.Equal(10, options.CircuitBreaker.FailureThreshold);
            Assert.Equal(60, options.CircuitBreaker.CircuitTimeoutSeconds);
            Assert.Equal(45, options.Timeout.TimeoutSeconds);
            Assert.Equal(100, options.Bulkhead.MaxParallelization);
        }

        [Fact]
        public async Task FullFlow_MultipleServiceInstances_ShareTelemetry()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var mockService = new Mock<IGoogleSheetsService>().Object;
            var provider = SetupServicesWithResilience(config, mockService);
            var telemetry = provider.GetRequiredService<IResilienceTelemetry>();

            // Act
            telemetry.IncrementSuccess();
            telemetry.IncrementRetry();
            var metrics1 = telemetry.GetMetrics();

            telemetry.IncrementSuccess();
            telemetry.IncrementTimeout();
            var metrics2 = telemetry.GetMetrics();

            // Assert - metrics accumulate in the shared telemetry
            Assert.Equal(1, metrics1.SuccessCount);
            Assert.Equal(1, metrics1.RetryCount);

            Assert.Equal(2, metrics2.SuccessCount);
            Assert.Equal(1, metrics2.RetryCount);
            Assert.Equal(1, metrics2.TimeoutCount);
        }
    }
}
