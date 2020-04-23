namespace Uruk.Server
{
    public interface IMerkleTree
    {
        byte[] Append(byte[] hash);
        //MerkleTree Build();
    }
}