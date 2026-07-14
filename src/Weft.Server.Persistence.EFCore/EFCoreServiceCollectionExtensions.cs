using Microsoft.EntityFrameworkCore;
using Weft.Server.Persistence;
using Weft.Server.Persistence.EFCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro DI del adaptador <see cref="EFCoreDocumentStore"/>.</summary>
public static class WeftEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="EFCoreDocumentStore"/> como <see cref="IDocumentStore"/> (singleton) sobre una
    /// factory de <see cref="WeftDocumentStoreContext"/>. El consumidor elige el provider en
    /// <paramref name="configureContext"/>, p. ej. <c>opts =&gt; opts.UseSqlite(connectionString)</c>.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="configureContext">Configura el <see cref="DbContextOptionsBuilder"/> (provider + conexión).</param>
    public static IServiceCollection AddWeftEFCoreDocumentStore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureContext);

        services.AddDbContextFactory<WeftDocumentStoreContext>(configureContext);
        services.AddSingleton<IDocumentStore, EFCoreDocumentStore>();
        return services;
    }
}
