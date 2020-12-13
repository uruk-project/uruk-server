using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using JsonWebToken.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public class MongoDBAuditTrailStore : IAuditTrailStore
    {
        private readonly IMongoClient _client;
        private readonly MongoDBStoreOptions _options;
        private readonly ILogger<MongoDBAuditTrailStore> _logger;
        private readonly IMerkleTree? _tree;
        private readonly IMongoCollection<AuditTrailBlock> _auditTrails;
        private readonly IMongoCollection<Keyring> _keyring;

        public MongoDBAuditTrailStore(IMongoClient client, IOptions<MongoDBStoreOptions> options, ILogger<MongoDBAuditTrailStore> logger, IMerkleTree? tree = null)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options.Value;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tree = tree;
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
                    new CreateIndexOptions() { Unique = true }
                );
            _keyring.Indexes.CreateOne(keyringIndexModel);
        }

        public async Task StoreAsync(AuditTrailRecord record, CancellationToken cancellationToken)
        {
            var hash = new byte[Sha256.Sha256HashSize];
            ComputeHash(record.Raw.Span, hash);
            SignedTreeHead? signedTreeHead = null;
            if (_tree != null)
            {
                signedTreeHead = await _tree.AppendAsync(hash);
            }
            
            var payload = record.Token.Payload!;
            
            // Is it posible to fail here ?
            MemoryMarshal.TryGetArray(record.Raw, out var segment);
            var block = new AuditTrailBlock
            {
                Iss = payload[JwtClaimNames.Iss.EncodedUtf8Bytes].GetString()!,
                Jti = payload[JwtClaimNames.Jti.EncodedUtf8Bytes].GetString()!,
                Iat = payload[JwtClaimNames.Iat.EncodedUtf8Bytes].GetInt64(),
                Aud = payload[JwtClaimNames.Aud.EncodedUtf8Bytes].GetStringArray()!,
                Txn = payload[SecEventClaimNames.Txn].GetString(),
                Toe = payload[SecEventClaimNames.Toe].GetInt64(),
                Events = payload[SecEventClaimNames.Events.EncodedUtf8Bytes].GetJsonDocument(),
                Raw = segment.Array!,
                Hash = hash
                signedTreeHead?.Hash
            };

            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
            try
            {
                session.StartTransaction();
                await _auditTrails.InsertOneAsync(block, cancellationToken: cancellationToken);

                //var kid = record.Token.Header[JwtHeaderParameterNames.Kid.EncodedUtf8Bytes].GetString()!;
                //if (!_keyringInMemory.HasKey(kid))
                //{
                //    var filter = Builders<Keyring>.Filter.Eq("keys.kid", kid);
                //    var storedKey = await _keyring.Find(filter).FirstOrDefaultAsync(cancellationToken);
                //    if (storedKey is null)
                //    {
                //        var key = SerializeKey(record.Token.SigningKey!);
                //        var jwksFilter = Builders<Keyring>.Filter.Eq("iss", record.Issuer);
                //        var push = Builders<Keyring>.Update.Push("keys", key)
                //            .SetOnInsert("iss", record.Issuer);
                //        await _keyring.UpdateOneAsync(jwksFilter, push, new UpdateOptions { IsUpsert = true }, cancellationToken);
                //    }
                //}

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

            _logger.LogInformation("'{Jti}' has been recorded.", block.Jti);
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
    }
}