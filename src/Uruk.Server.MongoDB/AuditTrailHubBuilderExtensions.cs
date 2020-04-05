using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public static class AuditTrailHubBuilderExtensions
    {
        public static IAuditTrailHubBuilder AddMongoDB(this IAuditTrailHubBuilder builder, string connectionString, bool verifyDuplicates = false)
        {
            return builder.AddMongoDB(MongoClientSettings.FromConnectionString(connectionString), verifyDuplicates);
        } 
        
        public static IAuditTrailHubBuilder AddMongoDB(this IAuditTrailHubBuilder builder, MongoClientSettings settings, bool verifyDuplicates = false)
        {
            builder.Services.TryAddSingleton<IAuditTrailStore, MongoDBAuditTrailStore>();
            builder.Services.AddOptions<MongoDBStoreOptions>();
            var client = new MongoClient(settings);
                
            builder.Services.TryAddSingleton<IMongoClient>(client);

            var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("camelCase", conventionPack, t => t.Namespace?.StartsWith("Uruk") ?? false);
            if (verifyDuplicates)
            {
                builder.Services.TryAddSingleton<IDuplicateStore, MongoDBDuplicateStore>();
            }

            return builder;
        }
    }
}
