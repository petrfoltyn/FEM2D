using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FEALiTE2D.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FealiteApiClient"/> as a typed HttpClient bound to
    /// <see cref="FealiteApiClientOptions"/> from configuration section <c>FealiteApi</c>.
    /// </summary>
    public static IServiceCollection AddFealiteApiClient(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<FealiteApiClientOptions>()
                .Bind(configuration.GetSection(FealiteApiClientOptions.SectionName))
                .ValidateOnStart();

        return RegisterClient(services);
    }

    /// <summary>
    /// Registers <see cref="FealiteApiClient"/> with explicit options (useful for tests).
    /// </summary>
    public static IServiceCollection AddFealiteApiClient(this IServiceCollection services,
        Action<FealiteApiClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<FealiteApiClientOptions>().Configure(configure).ValidateOnStart();
        return RegisterClient(services);
    }

    private static IServiceCollection RegisterClient(IServiceCollection services)
    {
        services.AddHttpClient<FealiteApiClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<FealiteApiClientOptions>>().Value;
            if (options.BaseAddress is null)
                throw new InvalidOperationException("FealiteApiClientOptions.BaseAddress is not configured.");
            http.BaseAddress = options.BaseAddress;
            http.Timeout = options.Timeout;
        });
        return services;
    }
}
