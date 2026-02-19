using GoogleSheetsService.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GoogleSheetsService.Tests.Resilience
{
    /// <summary>
    /// Integration tests for resilience DI registration and decorator pattern.
    /// </summary>
    public class ResilienceDependencyInjectionTests
    {
        private IConfigurationBuilder CreateConfigurationBuilder()
        {
            return new ConfigurationBuilder();
        }

        /// <summary>
        /// Creates a service collection with the minimal dependencies needed for GoogleSheetsService.
        /// GoogleSheetsService needs ILogger (non-generic) and ISheetsServiceWrapper.
        /// </summary>
        private static ServiceCollection CreateBaseServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton<ISheetsServiceWrapper>(new Mock<ISheetsServiceWrapper>().Object);
            return services;
        }

        [Fact]
        public void AddGoogleSheetsService_RegistersRequiredServices()
        {
            // Arrange
            var config = CreateConfigurationBuilder().Build();
            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(provider.GetRequiredService<IGoogleSheetsService>());
            Assert.NotNull(provider.GetRequiredService<IResiliencePipelineFactory>());
            Assert.NotNull(provider.GetRequiredService<IResilienceTelemetry>());
        }

        [Fact]
        public void AddGoogleSheetsService_WithNoResilienceConfig_DoesNotApplyResilience()
        {
            // Arrange
            var config = CreateConfigurationBuilder().Build();
            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            // Assert - service should be unwrapped (original GoogleSheetsService, not decorated)
            Assert.NotNull(service);
            Assert.IsType<GoogleSheetsService>(service);
        }

        [Fact]
        public void AddGoogleSheetsService_WithRetryEnabled_AppliesResilience()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            // Assert - service should be decorated
            Assert.NotNull(service);
            Assert.IsType<GoogleSheetsServiceWithResilience>(service);
        }

        [Fact]
        public void AddGoogleSheetsService_WithCircuitBreakerEnabled_AppliesResilience()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:CircuitBreaker:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            // Assert - service should be decorated
            Assert.IsType<GoogleSheetsServiceWithResilience>(service);
        }

        [Fact]
        public void AddGoogleSheetsService_WithTimeoutEnabled_AppliesResilience()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Timeout:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            // Assert
            Assert.IsType<GoogleSheetsServiceWithResilience>(service);
        }

        [Fact]
        public void AddGoogleSheetsService_WithBulkheadEnabled_AppliesResilience()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Bulkhead:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IGoogleSheetsService>();

            // Assert
            Assert.IsType<GoogleSheetsServiceWithResilience>(service);
        }

        [Fact]
        public void AddGoogleSheetsService_WithMultiplePoliciesEnabled_AppliesResilience()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Retry:MaxRetries", "5" },
                { "GoogleSheetsService:Resilience:CircuitBreaker:Enabled", "true" },
                { "GoogleSheetsService:Resilience:CircuitBreaker:FailureThreshold", "10" },
                { "GoogleSheetsService:Resilience:Timeout:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Timeout:TimeoutSeconds", "45" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IGoogleSheetsService>();
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>().Value;

            // Assert
            Assert.IsType<GoogleSheetsServiceWithResilience>(service);
            Assert.True(options.Retry.Enabled);
            Assert.Equal(5, options.Retry.MaxRetries);
            Assert.True(options.CircuitBreaker.Enabled);
            Assert.Equal(10, options.CircuitBreaker.FailureThreshold);
            Assert.True(options.Timeout.Enabled);
            Assert.Equal(45, options.Timeout.TimeoutSeconds);
        }

        [Fact]
        public void AddGoogleSheetsService_ConfigurationOptionsAreReadFromConfig()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Retry:MaxRetries", "7" },
                { "GoogleSheetsService:Resilience:Retry:InitialDelayMs", "250" },
                { "GoogleSheetsService:Resilience:Retry:MaxDelayMs", "10000" },
                { "GoogleSheetsService:Resilience:Retry:BackoffMultiplier", "3.0" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>().Value;

            // Assert
            Assert.True(options.Retry.Enabled);
            Assert.Equal(7, options.Retry.MaxRetries);
            Assert.Equal(250, options.Retry.InitialDelayMs);
            Assert.Equal(10000, options.Retry.MaxDelayMs);
            Assert.Equal(3.0, options.Retry.BackoffMultiplier);
        }

        [Fact]
        public void AddGoogleSheetsService_TelemetryRegisteredAsSingleton()
        {
            // Arrange
            var config = CreateConfigurationBuilder().Build();
            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var telemetry1 = provider.GetRequiredService<IResilienceTelemetry>();
            var telemetry2 = provider.GetRequiredService<IResilienceTelemetry>();

            // Assert - should be same instance
            Assert.Same(telemetry1, telemetry2);
        }

        [Fact]
        public void AddGoogleSheetsService_FactoryRegisteredAsSingleton()
        {
            // Arrange
            var config = CreateConfigurationBuilder().Build();
            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var factory1 = provider.GetRequiredService<IResiliencePipelineFactory>();
            var factory2 = provider.GetRequiredService<IResiliencePipelineFactory>();

            // Assert - should be same instance
            Assert.Same(factory1, factory2);
        }

        [Fact]
        public void AddGoogleSheetsService_ServiceRegisteredAsScoped()
        {
            // Arrange
            var config = CreateConfigurationBuilder().Build();
            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            using (var scope1 = provider.CreateScope())
            using (var scope2 = provider.CreateScope())
            {
                var service1 = scope1.ServiceProvider.GetRequiredService<IGoogleSheetsService>();
                var service2 = scope2.ServiceProvider.GetRequiredService<IGoogleSheetsService>();

                // Assert - should be different instances for different scopes
                Assert.NotSame(service1, service2);
            }
        }

        [Fact]
        public void AddGoogleSheetsService_ResilienceFactoryConfiguresAllPolicies()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "GoogleSheetsService:Resilience:Retry:Enabled", "true" },
                { "GoogleSheetsService:Resilience:CircuitBreaker:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Timeout:Enabled", "true" },
                { "GoogleSheetsService:Resilience:Bulkhead:Enabled", "true" }
            };
            var config = CreateConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = CreateBaseServices();

            // Act
            services.AddGoogleSheetsService(config);
            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IResiliencePipelineFactory>();

            // Assert
            var pipeline = factory.CreatePipeline("GoogleSheets");
            Assert.NotNull(pipeline);
        }
    }
}
