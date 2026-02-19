namespace GoogleSheetsService;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resilience;

public static class Extensions
{
    /// <summary>
    /// Adds GoogleSheetsService to the dependency injection container with optional Polly resilience.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
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
        services.AddSingleton<IResiliencePipelineFactory>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            var logger = provider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("GoogleSheetsService.Resilience");
            return new ResiliencePipelineFactory(options, logger);
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

            var pipelineFactory = provider.GetRequiredService<IResiliencePipelineFactory>();
            var logger = provider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<GoogleSheetsServiceWithResilience>();
            var pipeline = pipelineFactory.CreatePipeline("GoogleSheets");

            return new GoogleSheetsServiceWithResilience(inner, pipeline, logger);
        });

        return services;
    }

    /// <summary>
    /// Decorator pattern implementation for optional resilience wrapping.
    /// This method allows wrapping the service with additional functionality based on runtime conditions.
    /// </summary>
    private static IServiceCollection Decorate<TInterface>(
        this IServiceCollection services,
        Func<TInterface, IServiceProvider, TInterface> decorator)
        where TInterface : class
    {
        var wrappedDescriptor = services.FirstOrDefault(
            s => s.ServiceType == typeof(TInterface)) ??
            throw new InvalidOperationException($"{typeof(TInterface).Name} is not registered");

        var implType = wrappedDescriptor.ImplementationType ??
            throw new InvalidOperationException($"{typeof(TInterface).Name} has no implementation type");

        // Register the concrete implementation type directly so the DI container handles
        // constructor injection (avoids ActivatorUtilities ambiguity with multiple constructors).
        services.Add(ServiceDescriptor.Describe(implType, implType, wrappedDescriptor.Lifetime));

        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TInterface))
            {
                services.RemoveAt(i);
            }
        }
        services.Add(ServiceDescriptor.Describe(
            typeof(TInterface),
            provider =>
            {
                var impl = (TInterface)provider.GetRequiredService(implType);
                return decorator(impl, provider);
            },
            wrappedDescriptor.Lifetime));

        return services;
    }

    public static IList<IList<object>> ToGoogleSheetsValues<T>(this IEnumerable<T> list)
    {
        // Get the properties of the class
        var properties = typeof(T).GetProperties();

        // Convert the list of objects to an IList<IList<object>> using reflection
        return list.Select(p => properties.Select(prop => prop.GetValue(p)).ToList()).Cast<IList<object>>().ToList();
    }
}

