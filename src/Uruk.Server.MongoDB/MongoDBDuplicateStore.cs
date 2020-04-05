using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public class MongoDBDuplicateStore : IDuplicateStore
    {
        private readonly IMongoClient _client;
        private readonly MongoDBStoreOptions _options;
        private readonly ILogger<MongoDBDuplicateStore> _logger;
        private readonly IMongoCollection<Duplicate> _duplicates;

        public MongoDBDuplicateStore(IMongoClient client, IOptions<MongoDBStoreOptions> options, ILogger<MongoDBDuplicateStore> logger)
        {
            _client = client;
            _options = options.Value;
            _logger = logger;
            var db = _client.GetDatabase(_options.Database);
            _duplicates = db.GetCollection<Duplicate>("audit_trail_duplicate");
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var uniqueIndex = new CreateIndexModel<Duplicate>(Builders<Duplicate>.IndexKeys
                .Combine(
                    Builders<Duplicate>.IndexKeys.Ascending(a => a.Iss),
                    Builders<Duplicate>.IndexKeys.Ascending(a => a.Jti)),
                    new CreateIndexOptions { Unique = true }
                );
            _duplicates.Indexes.CreateOne(uniqueIndex);

            var ttlIndex = new CreateIndexModel<Duplicate>(
                    Builders<Duplicate>.IndexKeys.Ascending(a => a.Iat),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(1) }
                );
            _duplicates.Indexes.CreateMany(new[] { uniqueIndex, ttlIndex });
        }

        public async ValueTask<bool> TryAddAsync(AuditTrailRecord record, CancellationToken cancellationToken = default)
        {
            var duplicate = new Duplicate(record.Issuer, record.Token.Id!, record.Token.IssuedAt!.Value);
            try
            {
                await _duplicates.InsertOneAsync(duplicate, cancellationToken);
            }
            catch
            {
                _logger.LogWarning("Duplicate audit trail detected. {Iss} - {Jti}", duplicate.Iss, duplicate.Jti);
                return false;
            }

            return true;
        }

        private class Duplicate
        {
            public Duplicate(string iss, string jti, DateTime iat)
            {
                Iss = iss;
                Jti = jti;
                Iat = iat;
            }

            [BsonRequired]
            [BsonElement("iss")]
            public string Iss { get; }

            [BsonRequired]
            [BsonElement("jti")]
            public string Jti { get; }

            [BsonRequired]
            [BsonElement("iat")]
            public DateTime Iat { get; }
        }
    }
}