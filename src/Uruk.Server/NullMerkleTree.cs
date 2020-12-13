using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uruk.Server
{
    internal class NullMerkleTree : IMerkleTree
    {
        public Task<SignedTreeHead> AppendAsync(byte[] hash)
        {
            return Task.FromResult(new SignedTreeHead(hash, Array.Empty<byte>(), 0, string.Empty));        
        }

        public Task<List<byte[]>> GetConsistencyProofAsync(ulong treeSize1, ulong treeSize2 = 0)
        {
            return Task.FromResult(new List<byte[]>());
        }

        public Task<List<byte[]>> GetInclusionProofAsync(ulong leafIndex, ulong treeSize)
        {
            return Task.FromResult(new List<byte[]>());
        }

        public Task<byte[]> GetRootHashAsync(ulong tree_size = 0)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        public Task<ulong> GetTreeSizeAsync()
        {
            return Task.FromResult(0ul);
        }

        public Task<byte[]> HashFullAsync(ulong leftIndex, ulong rightIndex)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        public Task<bool> VerifyIntegrityAsync()
        {
            return Task.FromResult(true);
        }
    }
}