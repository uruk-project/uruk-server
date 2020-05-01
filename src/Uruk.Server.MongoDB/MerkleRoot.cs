using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    public class MerkleRoot
    {
        public MerkleRoot(MerkleNode node, byte[] hash)
        {
            NodeId = node.Id;
            Level = node.Level;
            Hash = hash;
        }

        public MerkleRoot(ObjectId id, ObjectId node, int level,  byte[] hash)
        {
            Id = id;
            NodeId = node;
            Level = level;
            Hash = hash;
        }

        [BsonElement("hash")]
        public byte[] Hash { get; }

        [BsonId]
        [BsonElement("id")]
        public ObjectId Id { get; set; }

        [BsonElement("node")]
        public ObjectId NodeId { get; }

        [BsonElement("level")]
        public int Level { get; }
    }
}