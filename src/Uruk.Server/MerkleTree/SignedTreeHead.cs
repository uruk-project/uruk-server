using System;

namespace Uruk.Server
{
    public sealed class SignedTreeHead
    {
        public SignedTreeHead(byte[] hash, byte[] signature, ulong treeSize, string bucket)
        {
            TreeSize = treeSize;
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
            Bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        }

        public ulong TreeSize { get; }

        public byte[] Hash { get; }

        public byte[] Signature { get; }

        public string Bucket { get; }
    }
}