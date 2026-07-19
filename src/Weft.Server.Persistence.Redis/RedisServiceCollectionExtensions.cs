using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Weft.Server.Persistence;
using Weft.Server.Persistence.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for the <see cref="RedisDocumentStore"/> adapter.</summary>
public static class WeftRedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RedisDocumentStore"/> as <see cref="IDocumentStore"/> (singleton). If no
    /// <see cref="IConnectionMultiplexer"/> is already registered, creates one with <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration"><c>StackExchange.Redis</c> connection string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="keyPrefix">Key prefix to isolate the instance/environment.</param>
    public static IServiceCollection AddWeftRedisDocumentStore(
        this IServiceCollection services,
        string configuration,
        string keyPrefix = "weft:doc:")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(configuration);
        ArgumentNullException.ThrowIfNull(keyPrefix);

        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration));
        services.AddSingleton<IDocumentStore>(sp =>
            new RedisDocumentStore(sp.GetRequiredService<IConnectionMultiplexer>(), keyPrefix));
        return services;
    }
}
