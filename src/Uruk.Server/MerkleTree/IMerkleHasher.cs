using System;

namespace Uruk.Server
{
    public interface IMerkleHasher
    {
        byte[] HashEmpty();

        byte[] HashLeaf(ReadOnlySpan<byte> leaf);

        byte[] HashNode(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);
    }
}