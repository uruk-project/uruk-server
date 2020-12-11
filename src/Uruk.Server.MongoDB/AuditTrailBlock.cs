using JsonWebToken;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    internal class AuditTrailBlock
    {
        public AuditTrailBlock(string iss, string jti, long iat, string[] aud, string? txn, long? toe, JwtObject events, byte[] raw, byte[] hash, byte[]? rootHash)
        {
            Iss = iss;
            Jti = jti;
            Iat = iat;
            Aud = aud;
            Txn = txn;
            Toe = toe;
            Events = events;
            Raw = raw;
            Hash = hash;
            RootHash = rootHash;
        }

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

        [BsonRequired]
        [BsonElement("r_hash")]
        public byte[]? RootHash { get; set; }
    }
}