using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    public class MerkleRoot
    {
        public MerkleRoot(int level, byte[] hash, ulong treeSize, byte[] signature, string bucket)
        {
            Level = level;
            Hash = hash;
            TreeSize = treeSize;
            Signature = signature;
            Bucket = bucket;
        }

        [BsonId]
        [BsonElement("hash")]
        public byte[] Hash { get; }

        [BsonElement("tree_size")]
        public ulong TreeSize { get; }

        [BsonElement("level")]
        public int Level { get; }

        [BsonElement("sig")]
        public byte[] Signature { get; }

        [BsonElement("bucket")]
        public string Bucket { get; }
    }
}