using System.Threading;
using System.Threading.Tasks;

namespace Uchat.Database.LiteDB;

public interface ILiteDbBackupUploader
{
    Task UploadAsync(string backupFilePath, CancellationToken cancellationToken = default);
}
