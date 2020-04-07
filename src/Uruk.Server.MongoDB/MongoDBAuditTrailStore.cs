using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public class MongoDBAuditTrailStore : IAuditTrailStore
    {
        private readonly IMongoClient _client;
        private readonly MongoDBStoreOptions _options;
        private readonly ILogger<MongoDBAuditTrailStore> _logger;
        private readonly IMongoCollection<AuditTrailBlock> _auditTrails;
        private readonly IMongoCollection<Keyring> _keyring;

        public MongoDBAuditTrailStore(IMongoClient client, IOptions<MongoDBStoreOptions> options, ILogger<MongoDBAuditTrailStore> logger)
        {
            _client = client;
            _options = options.Value;
            _logger = logger;
            var db = _client.GetDatabase(_options.Database);
            _auditTrails = db.GetCollection<AuditTrailBlock>("audit_trail");
            _keyring = db.GetCollection<Keyring>("keyring");

            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var auditTrailIndexModel = new CreateIndexModel<AuditTrailBlock>(Builders<AuditTrailBlock>.IndexKeys
                .Combine(
                    Builders<AuditTrailBlock>.IndexKeys.Ascending(a => a.Iss),
                    Builders<AuditTrailBlock>.IndexKeys.Ascending(a => a.Jti)),
                    new CreateIndexOptions { Unique = true }
                );
            _auditTrails.Indexes.CreateOne(auditTrailIndexModel);

            var keyringIndexModel = new CreateIndexModel<Keyring>(
                    Builders<Keyring>.IndexKeys.Ascending(a => a.Iss),
                    new CreateIndexOptions { Unique = true }
                );
            _keyring.Indexes.CreateOne(keyringIndexModel);
        }

        private static void ComputeHash(ReadOnlySpan<byte> source, Span<byte> destination)
            => Sha256.Shared.ComputeHash(source, destination);

        public async Task StoreAsync(AuditTrailRecord record, CancellationToken cancellationToken)
        {
            var hash = new byte[Sha256.Shared.HashSize];
            ComputeHash(record.Raw, hash);
            var block = new AuditTrailBlock
            {
                Iss = record.Issuer,
                Jti = record.Token.Payload!.Jti!,
                Iat = record.Token.Payload.Iat!.Value,
                Aud = record.Token.Payload.Aud,
                Txn = record.Token.TransactionNumber,
                Toe = record.Token.Payload.TryGetValue(SetClaims.ToeUtf8, out var toe) ? (long?)toe.Value : default,
                Events = record.Token.Events,
                Raw = record.Raw,
                Hash = hash
            };

            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
            try
            {
                session.StartTransaction();
                await _auditTrails.InsertOneAsync(block, cancellationToken: cancellationToken);
                if (record.Token.SigningKey!.Kid is null)
                {
                    record.Token.SigningKey.Kid = Encoding.UTF8.GetString(record.Token.SigningKey.ComputeThumbprint());
                }

                var filter = Builders<Keyring>.Filter.Eq("keys.kid", record.Token.SigningKey!.Kid);
                var storedKey = await _keyring.Find(filter).FirstOrDefaultAsync(cancellationToken);
                if (storedKey is null)
                {
                    var key = SerializeKey(record.Token.SigningKey!);
                    var jwksFilter = Builders<Keyring>.Filter.Eq("iss", record.Issuer);
                    var push = Builders<Keyring>.Update.Push("keys", key)
                        .SetOnInsert("iss", record.Issuer);
                    await _keyring.UpdateOneAsync(jwksFilter, push, new UpdateOptions { IsUpsert = true }, cancellationToken);
                }

                await session.CommitTransactionAsync(cancellationToken);
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                _logger.LogWarning("Duplicate document. The document will be ignored.");
                await session.AbortTransactionAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to store the document.");
                await session.AbortTransactionAsync();
            }

            _logger.LogInformation("'{Jti}' has been recorded", block.Jti);
        }

        private unsafe static BsonDocument SerializeKey(Jwk key)
        {
            using var bufferWriter = new PooledByteBufferWriter(1024);
            using var jsonWriter = new Utf8JsonWriter(bufferWriter);
            jsonWriter.WriteStartObject();
            key.WriteTo(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            return BsonDocument.Parse(Encoding.UTF8.GetString(bufferWriter.WrittenSpan));
        }

        private class AuditTrailBlock
        {
            [BsonRequired]
            [BsonElement("iss")]
            public string Iss { get; set; }

            [BsonRequired]
            [BsonElement("jti")]
            public string Jti { get; set; }

            [BsonRequired]
            [BsonElement("iat")]
            public long Iat { get; set; }

            [BsonElement("aud")]
            [BsonIgnoreIfNull]
            public string[] Aud { get; set; }

            [BsonElement("txn")]
            [BsonIgnoreIfNull]
            public string? Txn { get; set; }

            [BsonElement("toe")]
            [BsonIgnoreIfNull]
            public long? Toe { get; set; }

            [BsonRequired]
            [BsonElement("raw")]
            public byte[] Raw { get; set; }

            [BsonRequired]
            [BsonElement("events")]
            public JwtObject Events { get; set; }

            [BsonRequired]
            [BsonElement("hash")]
            public byte[] Hash { get; set; }
        }

        private class Keyring
        {
            [BsonRequired]
            [BsonElement("iss")]
            public string Iss { get; set; }

            [BsonRequired]
            [BsonElement("keys")]
            public BsonDocument Keys { get; set; }
        }
    }
}