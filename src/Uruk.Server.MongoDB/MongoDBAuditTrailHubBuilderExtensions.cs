using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public static class MongoDBAuditTrailHubBuilderExtensions
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

            BsonClassMap.RegisterClassMap<AuditTrailBlock>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(b => new AuditTrailBlock(b.Iss, b.Jti, b.Iat, b.Aud, b.Txn, b.Toe, b.Events, b.Raw, b.Hash));
            });
            BsonClassMap.RegisterClassMap<Keyring>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(k => new Keyring(k.Iss, k.Keys));
            });


            BsonClassMap.RegisterClassMap<MerkleNode>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(n => new MerkleNode(n.Children, n.Level, n.Hash, n.IsFull));
            });
            BsonClassMap.RegisterClassMap<MerkleRoot>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(r => new MerkleRoot(r.Id, r.NodeId, r.Level, r.Hash));
            });

            return builder;
        }
    }
}
