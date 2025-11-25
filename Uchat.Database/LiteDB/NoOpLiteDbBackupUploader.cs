using System.Threading;
using System.Threading.Tasks;

namespace Uchat.Database.LiteDB;

public sealed class NoOpLiteDbBackupUploader : ILiteDbBackupUploader
{
    public Task UploadAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
