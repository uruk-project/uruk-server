using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public class MongoDBAuditTrailStore : IAuditTrailStore
    {
        private readonly ILogger<MongoDBAuditTrailStore> _logger;

        public MongoDBAuditTrailStore(ILogger<MongoDBAuditTrailStore> logger)
        {
            _logger = logger;
        }

        private static void ComputeHash(ReadOnlySpan<byte> source, Span<byte> destination)
            => Sha256.Shared.ComputeHash(source, destination);

        public async ValueTask<bool> CheckDuplicateAsync(string issuer, string id, string clientId)
        {
            var client = new MongoClient();
            var db = client.GetDatabase("uruk");
            var auditTrails = db.GetCollection<AuditTrailBlock>("audit_trail");
            var filter = Builders<AuditTrailBlock>.Filter.Eq(a => a.Iss, issuer) & Builders<AuditTrailBlock>.Filter.Eq(a => a.Jti, id);
            var count = await auditTrails.CountDocumentsAsync(filter, new CountOptions { Limit = 1 });
            return count > 0;
        }

        public async Task StoreAsync(AuditTrailRecord record)
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

            var client = new MongoClient();
            var db = client.GetDatabase("uruk");
            var auditTrails = db.GetCollection<AuditTrailBlock>("audit_trail");
            var keyring = db.GetCollection<BsonDocument>("keyring");

            using var session = await client.StartSessionAsync();
            try
            {
                session.StartTransaction();
                await auditTrails.InsertOneAsync(block);
                if (record.Token.SigningKey!.Kid is null)
                {
                    record.Token.SigningKey.Kid = Encoding.UTF8.GetString(record.Token.SigningKey.ComputeThumbprint());
                }

                var filter = Builders<BsonDocument>.Filter.Eq("keys.kid", record.Token.SigningKey!.Kid);
                var storedKey = await keyring.Find(filter).FirstOrDefaultAsync();
                if (storedKey is null)
                {
                    var key = SerializeKey(record.Token.SigningKey!);
                    var jwksFilter = Builders<BsonDocument>.Filter.Eq("iss", record.Issuer);
                    var push = Builders<BsonDocument>.Update.Push("keys", key)
                        .SetOnInsert("iss", record.Issuer);
                    await keyring.UpdateOneAsync(jwksFilter, push, new UpdateOptions { IsUpsert = true });
                }

                await session.CommitTransactionAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to store the document.");
                await session.AbortTransactionAsync();
            }
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