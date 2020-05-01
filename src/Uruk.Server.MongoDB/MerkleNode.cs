using System;
using JsonWebToken;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    public class MerkleNode
    {
        public MerkleNode(ObjectId left, ObjectId right, int level, byte[] hash)
        {
            Level = level;
            Hash = hash;
            Children = new[] { left, right };
        }

        public MerkleNode(ObjectId[] children, int level, byte[] hash, bool isFull)
        {
            Level = level;
            Hash = hash;
            Children = children;
            IsFull = isFull;
        }

        public MerkleNode(MerkleNode left, MerkleNode right)
        {
            Level = left.Level + 1;
            Hash = new byte[Sha256.Shared.HashSize];
            Sha256.Shared.ComputeHash(right.Hash, (ReadOnlySpan<byte>)left.Hash, Hash);
            Children = new[] { left.Id, right.Id };
            IsFull = left.Level == right.Level && left.IsFull && right.IsFull;
        }

        public MerkleNode(MerkleNode left, MerkleNode right, byte[] hash)
        {
            Level = left.Level + 1;
            Hash = hash;
            Children = new[] { left.Id, right.Id };
            IsFull = left.Level == right.Level && left.IsFull && right.IsFull;
        }

        public MerkleNode(byte[] hash)
        {
            Level = 0;
            Hash = hash;
            Children = Array.Empty<ObjectId>();
            IsFull = true;
        }

        [BsonElement("hash")]
        public byte[] Hash { get; set; }

        [BsonId]
        [BsonElement("id")]
        public ObjectId Id { get; set; }

        [BsonElement("level")]
        public int Level { get; }

        [BsonIgnore]
        public ObjectId Left => Children.Length > 0 ? Children[0] : default;

        [BsonIgnoreIfDefault]
        [BsonIgnore]
        public ObjectId Right => Children.Length > 1 ? Children[1] : default;

        [BsonElement("children")]
        [BsonIgnoreIfDefault]
        public ObjectId[] Children { get; }

        [BsonElement("full")]
        public bool IsFull { get; set; }

        [BsonIgnore]
        public bool IsLeaf => Children.Length == 0;
    }
}