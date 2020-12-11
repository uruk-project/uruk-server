using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    public class MerkleNode
    {
        public MerkleNode(byte[] left, byte[] right, int level, byte[] hash)
        {
            Level = level;
            Hash = hash;
            Children = new[] { left, right };
        }

        public MerkleNode(byte[][] children, int level, byte[] hash, bool isFull)
        {
            Level = level;
            Hash = hash;
            Children = children;
            IsFull = isFull;
        }

        public MerkleNode(byte[] leaf)
        {
            Level = 0;
            Hash = leaf;
            Children = Array.Empty<byte[]>();
            IsFull = true;
        }

        [BsonId]
        [BsonElement("hash")]
        public byte[] Hash { get; set; }

        [BsonElement("level")]
        public int Level { get; }

        [BsonIgnore]
        public byte[] Left => Children.Length > 0 ? Children[0] : Array.Empty<byte>();

        [BsonIgnoreIfDefault]
        [BsonIgnore]
        public byte[] Right => Children.Length > 1 ? Children[1] : Array.Empty<byte>();

        [BsonElement("children")]
        [BsonIgnoreIfDefault]
        public byte[][] Children { get; }

        [BsonElement("full")]
        public bool IsFull { get; set; }

        [BsonIgnore]
        public bool IsLeaf => Children.Length == 0;
    }
}