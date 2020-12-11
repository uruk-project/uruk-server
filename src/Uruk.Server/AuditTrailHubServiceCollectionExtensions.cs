using System.Runtime.InteropServices;
using System.Security.Cryptography;
using JsonWebToken;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Uruk.Server;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides extension methods for registering <see cref="AuditTrailHubService"/> in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class AuditTrailHubServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the <see cref="AuditTrailHubService"/> to the container, using the provided delegate to register
        /// event receiver.
        /// </summary>
        /// <remarks>
        /// This operation is idempotent - multiple invocations will still only result in a single
        /// <see cref="AuditTrailHubService"/> instance in the <see cref="IServiceCollection"/>. It can be invoked
        /// multiple times in order to get access to the <see cref="IAuditTrailHubBuilder"/> in multiple places.
        /// </remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the <see cref="AuditTrailHubService"/> to.</param>
        /// <param name="audience">The audience.</param>
        /// <returns>An instance of <see cref="IAuditTrailHubBuilder"/> from which event receiver can be registered.</returns>
        public static IAuditTrailHubBuilder AddAuditTrailHub(this IServiceCollection services, string audience)
        {
            services.TryAddSingleton<IAuditTrailHubService, AuditTrailHubService>();
            services.AddHostedService<AuditTrailStorageBackgroundService>();
            services.TryAddSingleton<IAuditTrailSink, DefaultAuditTrailSink>();
            services.TryAddSingleton<IMerkleTree, NullMerkleTree>();

            services.AddOptions<AuditTrailHubOptions>()
                .PostConfigure(options =>
                {
                    options.Registry.Configure(audience);
                });

            return new AuditTrailHubBuilder(services);
        }

        public static IAuditTrailHubBuilder AddFileSystemStorage(this IAuditTrailHubBuilder builder)
        {
            builder.Services.TryAddSingleton<IAuditTrailStore, FileSystemAuditTrailStore>();
    
            return builder;
        }
    }

    public enum SupportedHashAlgorithm
    {
        Sha256,
        Sha384,
        Sha512
    }
}