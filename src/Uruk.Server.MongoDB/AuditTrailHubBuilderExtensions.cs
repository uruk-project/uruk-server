using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization.Conventions;

namespace Uruk.Server.MongoDB
{
    public static class AuditTrailHubBuilderExtensions
    {
        public static IAuditTrailHubBuilder AddMongoDB(this IAuditTrailHubBuilder builder)
        {
            builder.Services.TryAddSingleton<IAuditTrailStore, MongoDBAuditTrailStore>();
            var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("camelCase", conventionPack, t => t.Namespace?.StartsWith("Uruk") ?? false);

            return builder;
        }
    }
}
