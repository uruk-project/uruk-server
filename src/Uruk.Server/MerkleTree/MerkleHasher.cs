using System;
using JsonWebToken.Cryptography;

namespace Uruk.Server
{
    public class MerkleHasher : IMerkleHasher
    {
        private static ReadOnlySpan<byte> LeafPrepend => new byte[1] { 0x00 };
        private readonly Sha2 _sha;

        public MerkleHasher(Sha2 sha)
        {
            _sha = sha;
        }

        public byte[] HashNode(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            var hash = new byte[_sha.HashSize];
            Span<byte> l = stackalloc byte[left.Length + 1];
            left.CopyTo(l.Slice(1));
            l[0] = 0x01;
            _sha.ComputeHash(right, l, hash);

            return hash;
        }

        public byte[] HashLeaf(ReadOnlySpan<byte> leaf)
        {
            var hash = new byte[_sha.HashSize];
            _sha.ComputeHash(leaf, LeafPrepend, hash);

            return hash;
        }

        public byte[] HashEmpty()
        {
            var hash = new byte[_sha.HashSize];
            _sha.ComputeHash(ReadOnlySpan<byte>.Empty, hash);

            return hash;
        }
    }
}