using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Uruk.Server
{
    public class FileSystemDuplicateStore : IDuplicateStore
    {
        private readonly FileSystemStorageOptions _options;

        public FileSystemDuplicateStore(IOptions<FileSystemStorageOptions> options)
        {
            _options = options.Value;
        }

        public ValueTask<bool> TryAddAsync(AuditTrailRecord record, CancellationToken cancellationToken = default)
        {
            // TODO : check for path traversal !
            var path = Path.Combine(_options.Directory, record.ClienId, record.Token.Id!);
            return new ValueTask<bool>(!File.Exists(path));
        }
    }
}
