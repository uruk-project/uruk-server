using System;

namespace Uruk.Server
{
    public interface IMerkleSigner
    {
        byte[] Sign(byte[] data);
    }
}