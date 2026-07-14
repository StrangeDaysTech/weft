using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Weft.Server.Persistence;
using Weft.Server.Persistence.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro DI del adaptador <see cref="RedisDocumentStore"/>.</summary>
public static class WeftRedisServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="RedisDocumentStore"/> como <see cref="IDocumentStore"/> (singleton). Si no hay un
    /// <see cref="IConnectionMultiplexer"/> ya registrado, crea uno con <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="configuration">Cadena de conexión de <c>StackExchange.Redis</c> (p. ej. <c>localhost:6379</c>).</param>
    /// <param name="keyPrefix">Prefijo de claves para aislar la instancia/entorno.</param>
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
