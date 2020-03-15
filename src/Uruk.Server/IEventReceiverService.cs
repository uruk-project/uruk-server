using System.Buffers;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Server
{
    public interface IEventReceiverService
    {
        public Task<TokenResponse> TryStoreToken(ReadOnlySequence<byte> buffer, TokenValidationPolicy policy);
    }
}
