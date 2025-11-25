using System;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Uchat.Database.LiteDB;

/// <summary>
/// Performs scheduled cleanup of LiteDB message documents.
/// </summary>
public class MessageCleanupService : BackgroundService
{
    private readonly LiteDbSettings _settings;
    private readonly ILogger<MessageCleanupService> _logger;
    private readonly TimeSpan _interval;

    public MessageCleanupService(IOptions<LiteDbSettings> options, ILogger<MessageCleanupService> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, _settings.CleanupIntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting message cleanup service (retention: {RetentionDays}d, interval: {Interval}m)",
            _settings.RetentionDays,
            _interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-_settings.RetentionDays);
                var connection = new ConnectionString
                {
                    Filename = _settings.DatabasePath,
                    Connection = ConnectionType.Shared
                };

                using var cleanupDb = new LiteDatabase(connection);
                var messages = cleanupDb.GetCollection<LiteDbMessage>(_settings.MessagesCollectionName);
                var deletedCount = messages.DeleteMany(m => m.SentAt < cutoff);

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Deleted {DeletedCount} messages older than {Cutoff}", deletedCount, cutoff);
                }
                else
                {
                    _logger.LogDebug("No messages older than {Cutoff} found", cutoff);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up old LiteDB messages");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Host is stopping.
            }
        }

        _logger.LogInformation("Message cleanup service is stopping");
    }
}
