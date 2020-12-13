using System;
using System.Security.Cryptography;

namespace Uruk.Server
{
    public class ECDsaMerkleSigner : IMerkleSigner, IDisposable
    {
        private readonly ECDsa _signer;

        public ECDsaMerkleSigner(ECParameters parameters)
        {
            _signer = ECDsa.Create(parameters);
        }

        public void Dispose()
        {
            _signer.Dispose();
        }

        public byte[] Sign(byte[] hash)
        {
            return _signer.SignHash(hash);
        }
    }
}