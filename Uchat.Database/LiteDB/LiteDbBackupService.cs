using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Uchat.Database.LiteDB;

namespace Uchat.Database.LiteDB;

public class LiteDbBackupService : BackgroundService
{
    private readonly LiteDbSettings _settings;
    private readonly ILiteDbWriteGate _writeGate;
    private readonly ILiteDbBackupUploader _uploader;
    private readonly ILogger<LiteDbBackupService> _logger;
    private readonly TimeSpan _interval;

    public LiteDbBackupService(
        IOptions<LiteDbSettings> settings,
        ILiteDbWriteGate writeGate,
        ILiteDbBackupUploader uploader,
        ILogger<LiteDbBackupService> logger)
    {
        _settings = settings.Value;
        _writeGate = writeGate;
        _uploader = uploader;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, _settings.BackupIntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LiteDB backup service running (interval {Interval})", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformBackupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create LiteDB backup");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("LiteDB backup service is stopping");
    }

    private async Task PerformBackupAsync(CancellationToken cancellationToken)
    {
        var sourcePath = _settings.DatabasePath;
        if (!File.Exists(sourcePath))
        {
            _logger.LogWarning("LiteDB database file not found at {Path}", sourcePath);
            return;
        }

        Directory.CreateDirectory(_settings.BackupDirectory);

        await using (await _writeGate.AcquireAsync(cancellationToken))
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var backupFileName = $"messages-{timestamp}.db.bak";
            var destination = Path.Combine(_settings.BackupDirectory, backupFileName);

            File.Copy(sourcePath, destination, overwrite: true);
            _logger.LogInformation("Created LiteDB backup {BackupPath}", destination);

            await _uploader.UploadAsync(destination, cancellationToken);
            RotateBackups();
        }
    }

    private void RotateBackups()
    {
        if (_settings.BackupRetention <= 0)
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(_settings.BackupDirectory, "*.db.bak")
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToList();

        foreach (var file in files.Skip(_settings.BackupRetention))
        {
            try
            {
                File.Delete(file);
                _logger.LogDebug("Rotated LiteDB backup {Backup}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup {Backup}", file);
            }
        }
    }

    public async Task RestoreAsync(string backupFileName, CancellationToken cancellationToken = default)
    {
        var backupPath = Path.Combine(_settings.BackupDirectory, backupFileName);
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("Backup not found", backupPath);
        }

        await using (await _writeGate.AcquireAsync(cancellationToken))
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            File.Copy(backupPath, tempPath, overwrite: true);
            File.Copy(tempPath, _settings.DatabasePath, overwrite: true);
            File.Delete(tempPath);

            _logger.LogInformation("LiteDB restored from backup {BackupFile}", backupPath);
        }
    }
}
