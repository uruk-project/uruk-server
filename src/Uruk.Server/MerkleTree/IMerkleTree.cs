using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IMerkleTree
    {
        Task<SignedTreeHead> AppendAsync(byte[] hash);
        Task<List<byte[]>> GetConsistencyProofAsync(ulong treeSize1, ulong treeSize2 = 0);
        Task<List<byte[]>> GetInclusionProofAsync(ulong leafIndex, ulong treeSize);
        Task<byte[]> GetRootHashAsync(ulong tree_size = 0);
        Task<ulong> GetTreeSizeAsync();
        Task<byte[]> HashFullAsync(ulong leftIndex, ulong rightIndex);
        Task<bool> VerifyIntegrityAsync();
    }
}