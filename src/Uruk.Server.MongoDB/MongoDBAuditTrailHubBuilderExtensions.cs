using System.Security.Cryptography;
using JsonWebToken;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public static class MongoDBAuditTrailHubBuilderExtensions
    {
        public static IAuditTrailHubBuilder AddMongoDBStorage(this IAuditTrailHubBuilder builder, string connectionString)
        {
            return builder.AddMongoDBStorage(MongoClientSettings.FromConnectionString(connectionString));
        }

        public static IAuditTrailHubBuilder AddMongoDBStorage(this IAuditTrailHubBuilder builder, MongoClientSettings settings)
        {
            builder.Services.TryAddSingleton<IAuditTrailStore, MongoDBAuditTrailStore>();
            builder.Services.TryAddSingleton<IMerkleTree, MongoDBMerkleTree>();
            builder.Services.AddOptions<MongoDBStoreOptions>();
            var client = new MongoClient(settings);

            builder.Services.TryAddSingleton<IMongoClient>(client);
            var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("camelCase", conventionPack, t => t.Namespace?.StartsWith("Uruk") ?? false);

            BsonClassMap.RegisterClassMap<AuditTrailBlock>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(b => new AuditTrailBlock(b.Iss, b.Jti, b.Iat, b.Aud, b.Txn, b.Toe, b.Events, b.Raw, b.Hash, b.RootHash));
            });
            BsonClassMap.RegisterClassMap<Keyring>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(k => new Keyring(k.ID, k.Iss, k.Keys));
            });

            BsonClassMap.RegisterClassMap<MerkleNode>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(n => new MerkleNode(n.Children, n.Level, n.Hash, n.IsFull));
            });
            BsonClassMap.RegisterClassMap<MerkleRoot>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(r => new MerkleRoot(r.Level, r.Hash, r.TreeSize, r.Signature, r.Bucket));
            });
            BsonClassMap.RegisterClassMap<MerkleLeaf>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(r => new MerkleLeaf(r.ID, r.Hash));
            });

            return builder;
        }

        public static IAuditTrailHubBuilder AddMongoDBMerkleTree(this IAuditTrailHubBuilder builder, SupportedHashAlgorithm hashAlgorithm, ECParameters signatureParameters)
        {
            Sha2 sha = hashAlgorithm switch
            {
                SupportedHashAlgorithm.Sha256 => Sha256.Shared,
                SupportedHashAlgorithm.Sha384 => Sha384.Shared,
                SupportedHashAlgorithm.Sha512 => Sha512.Shared,
                _ => Sha256.Shared
            };
            builder.Services.TryAddSingleton<IMerkleHasher>(new MerkleHasher(sha));
            builder.Services.TryAddSingleton<IMerkleSigner>(new ECDsaMerkleSigner(signatureParameters));
            builder.Services.Replace(new ServiceDescriptor(typeof(IMerkleTree), typeof(MongoDBMerkleTree), ServiceLifetime.Singleton));

            return builder;
        }
    }
}
