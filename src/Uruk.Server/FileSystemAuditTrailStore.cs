using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Uruk.Server
{
    public class FileSystemAuditTrailStore : IAuditTrailStore
    {
        private readonly FileSystemStorageOptions _options;

        public FileSystemAuditTrailStore(IOptions<FileSystemStorageOptions> options)
        {
            _options = options.Value;
        }

        public Task StoreAsync(AuditTrailRecord record, CancellationToken cancellationToken = default)
        {
            // TODO : check for path traversal !
            var path = Path.Combine(_options.Directory, record.ClienId);
            Directory.CreateDirectory(path);
            return File.WriteAllBytesAsync(Path.Combine(path, record.Token.Id!), record.Raw, cancellationToken);
        }
    }
}
