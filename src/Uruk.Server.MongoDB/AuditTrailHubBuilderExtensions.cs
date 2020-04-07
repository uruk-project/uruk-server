using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public static class AuditTrailHubBuilderExtensions
    {
        public static IAuditTrailHubBuilder AddMongoDB(this IAuditTrailHubBuilder builder, string connectionString)
        {
            return builder.AddMongoDB(MongoClientSettings.FromConnectionString(connectionString));
        } 
        
        public static IAuditTrailHubBuilder AddMongoDB(this IAuditTrailHubBuilder builder, MongoClientSettings settings)
        {
            builder.Services.TryAddSingleton<IAuditTrailStore, MongoDBAuditTrailStore>();
            builder.Services.AddOptions<MongoDBStoreOptions>();
            var client = new MongoClient(settings);
                
            builder.Services.TryAddSingleton<IMongoClient>(client);

            var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("camelCase", conventionPack, t => t.Namespace?.StartsWith("Uruk") ?? false);

            return builder;
        }
    }
}
