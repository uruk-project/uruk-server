namespace Uruk.Server
{
    public interface IMerkleTree
    {
        MerkleProof Append(byte[] hash);
    }
}