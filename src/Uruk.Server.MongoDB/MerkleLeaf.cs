using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    public class MerkleLeaf
    {
        public MerkleLeaf(ulong id, byte[] hash)
        {
            Hash = hash;
            ID = id;
        }

        [BsonElement("hash")]
        public byte[] Hash { get; set; }

        [BsonId]
        [BsonElement("id")]
        public ulong ID { get; }
    }
}