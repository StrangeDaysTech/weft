using Microsoft.EntityFrameworkCore;
using Weft.Server.Persistence;
using Weft.Server.Persistence.EFCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for the <see cref="EFCoreDocumentStore"/> adapter.</summary>
public static class WeftEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EFCoreDocumentStore"/> as <see cref="IDocumentStore"/> (singleton) over a
    /// <see cref="WeftDocumentStoreContext"/> factory. The consumer chooses the provider in
    /// <paramref name="configureContext"/>, e.g. <c>opts =&gt; opts.UseSqlite(connectionString)</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureContext">Configures the <see cref="DbContextOptionsBuilder"/> (provider + connection).</param>
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
