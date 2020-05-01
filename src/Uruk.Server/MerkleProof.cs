namespace Uruk.Server
{
    public class MerkleProof
    {
        public MerkleProof(byte[] hash)
        {
            Hash = hash;
        }

        public byte[] Hash { get; }
    }
}